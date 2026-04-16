using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhalesExchangeBackend.Controllers.InternalSupport;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Models;
using WhalesExchangeBackend.Services;
using WhalesExchangeBackend.Services.DataProvider;
using WhalesExchangeBackend.Services.ElectrumRpc;
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesExchangeBackend.SharedLib.Services.WebSocket;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Controllers;

/// <summary>
/// Controller for REST API of the backend.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI.")]
internal class RestApiController : InternalControllerBase
{
    /// <summary>Number of blocks to serve as a safe buffer before for a lockup address timeout scenario.</summary>
    /// <remarks>
    /// When the swap provider publishes the funding transaction with an output paying to the lockup address, the client must be provided with enough time to spend this output
    /// before the time lock on the output allows the swap provider to claim the money back. If the client learned about the the funding transaction just before the time lock
    /// expires, the client may fail to spend it on time.
    /// </remarks>
    private const int LockupAddressTimeoutBuffer = 5;

    /// <summary>Number of satoshis that are considered high enough to justify waiting for two confirmations instead of one.</summary>
    private const long TwoConfirmationAmountThresholdSats = 1_000_000;

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log;

    /// <summary>Provides access to the current <see cref="HttpContext"/>.</summary>
    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>Provider of access to swap providers and their offers in the database.</summary>
    private readonly SwapProviderRepository swapProviderRepository;

    /// <summary>Provider of access to swaps in the database.</summary>
    private readonly SwapRepository swapRepository;

    /// <summary>Client that communicates with Electrum RPC server.</summary>
    private readonly ElectrumRpcClient electrumRpcClient;

    /// <summary>Monitor of blockchain data fetched from the Electrum backend client.</summary>
    private readonly BlockchainDataMonitor blockchainDataMonitor;

    /// <summary>Manager of swap subscriptions.</summary>
    private readonly SubscriptionManager subscriptionManager;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current <see cref="HttpContext"/>.</param>
    /// <param name="swapProviderRepository">Provider of access to swap providers and their offers in the database.</param>
    /// <param name="swapRepository">Provider of access to swaps in the database.</param>
    /// <param name="electrumRpcClient">Client that communicates with Electrum RPC server.</param>
    /// <param name="blockchainDataMonitor">Monitor of blockchain data fetched from the Electrum backend client.</param>
    /// <param name="subscriptionManager">Manager of swap subscriptions.</param>
    public RestApiController(IHttpContextAccessor httpContextAccessor, SwapProviderRepository swapProviderRepository, SwapRepository swapRepository,
        ElectrumRpcClient electrumRpcClient, BlockchainDataMonitor blockchainDataMonitor, SubscriptionManager subscriptionManager)
    {
        this.httpContextAccessor = httpContextAccessor;
        HttpContext? context = this.httpContextAccessor.HttpContext;
        if (context is null)
            throw new SanityCheckException("HTTP context is null.");

        this.log = new(this.GetType().FullName!, $"{context.Connection.RemoteIpAddress}");

        this.swapProviderRepository = swapProviderRepository;
        this.swapRepository = swapRepository;
        this.electrumRpcClient = electrumRpcClient;
        this.blockchainDataMonitor = blockchainDataMonitor;
        this.subscriptionManager = subscriptionManager;

        this.blockchainDataMonitor.RegisterOnMonitoredAddressActionCallback(this.OnMonitoredAddressActionAsync);

        this.log.Debug("*$");
    }

    /// <inheritdoc cref="BlockchainDataMonitor.OnMonitoredAddressActionCallback"/>
    private async Task OnMonitoredAddressActionAsync(MonitoredAddressAction action, MonitoredAddress monitoredAddress, string? transactionId, string? transactionData,
        CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(action)}={action},{nameof(monitoredAddress)}='{monitoredAddress}',{nameof(transactionId)}='{transactionId}',{
            nameof(transactionData)}='{transactionData.ToBoundedString()}'");

        DbSwap? swap = null;
        try
        {
            switch (action)
            {
                case MonitoredAddressAction.InMempool:
                case MonitoredAddressAction.Confirmed:
                    if (transactionId is null)
                        throw new SanityCheckException($"Transaction ID is required for action {action}.");

                    bool isConfirmed = action == MonitoredAddressAction.Confirmed;
                    swap = await this.swapProviderRepository.FundingTransactionSetAsync(monitoredAddress.SwapId, isConfirmed, transactionId: transactionId,
                        transactionData: transactionData).ConfigureAwait(false);
                    break;

                case MonitoredAddressAction.Timeout:
                    swap = await this.swapProviderRepository.FundingTransactionTimeoutAsync(monitoredAddress.SwapId).ConfigureAwait(false);
                    break;

                default:
                    throw new SanityCheckException($"Invalid action provided {action}.");
            }
        }
        catch (DatabaseException e)
        {
            this.log.Error($"Exception occurred while trying to update database record of swap ID {monitoredAddress.SwapId}: {e}");
        }

        if (swap is not null)
        {
            SwapUpdate swapUpdate = SwapUpdate.FromDbSwap(swap);
            await this.subscriptionManager.PropagateSwapUpdateAsync(swapUpdate, cancellationToken).ConfigureAwait(false);
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Action that is executed when a list of swap providers is requested.
    /// </summary>
    /// <returns>Result of the action method.</returns>
    [HttpGet]
    [Route("get-swap-providers")]
    public async Task<IActionResult> GetSwapProvidersAsync()
    {
        this.log.Debug("*");

        IActionResult result;
        DateTime min20ago = DateTime.UtcNow.AddMinutes(-20);

        GetSwapProvidersResponse response;
        try
        {
            DbSwapProvider[] dbRecords = await this.swapProviderRepository.GetRecentAsync(min20ago).ConfigureAwait(false);

            RestSwapProvider[] providers = dbRecords
                .Select(RestSwapProvider.FromDbSwapProvider)
                .OrderByDescending(c => c.PoWBits)
                .ThenBy(c => c.Pubkey)
                .ToArray();

            response = new(providers);
        }
        catch (Exception e)
        {
            this.log.Error($"Exception occurred while getting list of swap providers: {e}");
            response = new($"Getting list of swap providers from the database failed. {e.Message}");
        }

        result = this.Ok(response);

        this.log.Debug("$");
        return result;
    }

    /// <summary>
    /// Action that is executed when a swap is requested.
    /// </summary>
    /// <param name="request">Swap request.</param>
    /// <returns>Result of the action method.</returns>
    [HttpPost]
    [Route("createswap")]
    public async Task<IActionResult> CreateSwapAsync([FromBody] CreateSwapRequest request)
    {
        this.log.Debug($"* {nameof(request)}='{request}'");

        HttpContext? context = this.httpContextAccessor.HttpContext;
        if (context is null)
            throw new SanityCheckException("HTTP context is null.");

        IActionResult result;
        CreateSwapResponse response;

        string providerPk = request.PairHash;
        DbSwapProvider? provider;
        try
        {
            provider = await this.swapProviderRepository.GetByPubkeyAsync(providerPk).ConfigureAwait(false);
        }
        catch (DatabaseException e)
        {
            this.log.Error($"Exception occurred while trying to get swap provider with pubkey '{providerPk}' from the database: {e}");
            response = new($"Getting swap provider with pubkey '{providerPk}' failed. {e.Message}");
            result = this.Ok(response);

            this.log.Debug("$<GET_PROVIDER_DB_ERROR>");
            return result;
        }

        if (provider is null)
        {
            string msg = $"Swap provider with pubkey '{providerPk}' cannot be found.";
            this.log.Error(msg);
            response = new(msg);
            result = this.Ok(response);

            this.log.Debug("$<PROVIDER_NOT_FOUND>");
            return result;
        }

        DbSwap? swap = null;
        bool failed = false;
        try
        {
            if ((request.Type == CreateSwapRequest.ForwardSwapTypeStr) && (request.OrderSide == CreateSwapRequest.ForwardSwapOrderSideStr))
            {
                if (request.Invoice is not null)
                {
                    if (request.RefundPublicKey is not null)
                    {
                        response = new("Forward swaps are not supported at the moment.");
                    }
                    else response = new($"'{nameof(request.RefundPublicKey)}' is mandatory for forward swaps.");
                }
                else response = new($"'{nameof(request.Invoice)}' is mandatory for forward swaps.");
            }
            else if ((request.Type == CreateSwapRequest.ReverseSwapTypeStr) && (request.OrderSide == CreateSwapRequest.ReverseSwapOrderSideStr))
            {
                if (request.InvoiceAmount is not null)
                {
                    if (request.ClaimPublicKey is not null)
                    {
                        swap = await this.swapRepository.InsertReverseAsync(providerPubkey: providerPk, amountToPaySats: request.InvoiceAmount.Value,
                            amountToReceiveSats: request.ExpectedAmount).ConfigureAwait(false);

                        long prepaymentSats = 2 * provider.MiningFeeReverseSat;
                        ElectrumSwapData electrumSwapData = await this.electrumRpcClient.ReverseSwapAsync(lnAmountSats: request.InvoiceAmount.Value,
                            onChainAmountSats: request.ExpectedAmount, prepaymentSats: prepaymentSats, preimageHash: request.PreimageHash, claimPk: request.ClaimPublicKey,
                            providerPk: providerPk, context.RequestAborted).ConfigureAwait(false);

                        SwapResponse swapResponse = new(id: swap.FrontendId, reverse: true, asset: "BTC", invoice: electrumSwapData.Invoice,
                            feeInvoice: electrumSwapData.FeeInvoice, timeoutBlockHeight: electrumSwapData.Locktime, sendAmountSats: electrumSwapData.LightningAmountSats,
                            receiveAmountSats: request.ExpectedAmount, onChainAmountSats: electrumSwapData.OnChainAmountSats, redeemScript: electrumSwapData.RedeemScriptHex,
                            lockupAddress: electrumSwapData.LockupAddress);

                        response = new(swapResponse);

                        int requiredConfirmations = this.GetRequiredConfirmationsForAmount(request.ExpectedAmount);
                        int timeoutHeight = (int)electrumSwapData.Locktime - requiredConfirmations - LockupAddressTimeoutBuffer;
                        this.blockchainDataMonitor.RegisterMonitoredAddress(swapId: swap.Id, electrumSwapData.LockupAddress, amountSats: request.ExpectedAmount,
                            requiredConfirmations: requiredConfirmations, timeoutHeight: timeoutHeight);

                        await this.swapRepository.MarkSwapAcceptedAsync(id: swap.Id, electrumSwapData.LockupAddress, timeoutBlockHeight: electrumSwapData.Locktime)
                            .ConfigureAwait(false);
                    }
                    else response = new($"'{nameof(request.ClaimPublicKey)}' is mandatory for reverse swaps.");
                }
                else response = new($"'{nameof(request.InvoiceAmount)}' is mandatory for reverse swaps.");
            }
            else response = new($"Unknown swap type '{request.Type}' and order side '{request.OrderSide}' combination.");
        }
        catch (Exception e)
        {
            this.log.Error($"Exception occurred while creating a new swap: {e}");
            response = new($"Creating new swap failed. {e.Message}");
            failed = true;
        }

        if (failed && (swap is not null))
        {
            try
            {
                await this.swapRepository.MarkSwapRejectedAsync(swap.Id).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.log.Error($"Exception occurred while marking swap ID {swap.Id} as rejected: {e}");
            }
        }

        result = this.Ok(response);

        this.log.Debug("$");
        return result;
    }

    /// <summary>
    /// Gets number of confirmations required for the funding transaction based on the amount being swapped.
    /// </summary>
    /// <param name="amountSats">Amount being swapped in satoshis.</param>
    /// <returns>Number of confirmations required for the funding transaction.</returns>
    private int GetRequiredConfirmationsForAmount(long amountSats)
        => amountSats >= TwoConfirmationAmountThresholdSats ? 2 : 1;

    /// <summary>
    /// Action that is executed when a swap status is requested.
    /// </summary>
    /// <param name="frontendId">Frontend ID of the swap.</param>
    /// <returns>Result of the action method.</returns>
    [HttpGet]
    [Route("v2/swap/{frontendId}")]
    public async Task<IActionResult> GetSwapStatusAsync(string frontendId)
    {
        this.log.Debug($"* {nameof(frontendId)}='{frontendId}'");

        IActionResult result;
        DbSwap?[] swaps = await this.swapRepository.GetSwapsByFrontendIdsAsync(new string[] { frontendId }).ConfigureAwait(false);

        if ((swaps.Length == 1) && (swaps[0] is not null))
        {
            SwapUpdate swapUpdate = SwapUpdate.FromDbSwap(swaps[0]!);

            GetSwapStatusResponse response = new(status: swapUpdate.Status, failureReason: swapUpdate.FailureReason, swapUpdate.Transaction, error: null);
            result = this.Ok(response);
        }
        else
        {
            this.log.Debug($"Unable to get swap status of swap frontend ID '{frontendId}'.");

            GetSwapStatusResponse response = new(status: null, failureReason: null, transaction: null, error: $"Could not find swap with ID '{frontendId}'.");
            result = this.NotFound(response);
        }

        this.log.Debug("$");
        return result;
    }

    /// <summary>
    /// Action that is executed when a swap's funding transaction is requested.
    /// </summary>
    /// <param name="request">Request to get funding transaction of a swap.</param>
    /// <returns>Result of the action method.</returns>
    [HttpPost]
    [Route("getswaptransaction")]
    public async Task<IActionResult> GetSwapTransactionAsync([FromBody] GetSwapTransactionRequest request)
    {
        this.log.Debug($"* {nameof(request)}='{request}'");

        string id = request.Id;
        IActionResult result;

        GetSwapTransactionResponse response;
        try
        {
            DbSwap?[] dbRecords = await this.swapRepository.GetSwapsByFrontendIdsAsync(new string[] { id }).ConfigureAwait(false);

            if (dbRecords.Length != 1)
                throw new SanityCheckException($"Expected 1 record, got {dbRecords.Length}");

            DbSwap? swap = dbRecords[0];
            if (swap is not null)
            {
                if (swap.FundingTxId is not null)
                {
                    response = new(transactionId: swap.FundingTxId, transactionData: swap.FundingTxData, timeoutBlockHeight: swap.TimeoutBlockHeight, error: null);
                }
                else
                {
                    this.log.Debug($"Swap frontend ID '{id}' does not have funding transaction yet.");
                    response = new($"No coins were locked up yet for swap '{id}'.");
                }
            }
            else
            {
                this.log.Debug($"Swap frontend ID '{id}' does not exist.");
                response = new($"Could not find swap with frontend ID '{id}'.");
            }
        }
        catch (Exception e)
        {
            this.log.Error($"Exception occurred while getting swap frontend ID '{id}' from the database: {e}");
            response = new($"Getting swap frontend ID '{id}' from the database failed. {e.Message}");
        }

        result = this.Ok(response);

        this.log.Debug("$");
        return result;
    }
}