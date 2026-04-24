using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhalesExchangeBackend.Controllers.InternalSupport;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Models;
using WhalesExchangeBackend.Services;
using WhalesExchangeBackend.Services.ElectrumRpc;
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesExchangeBackend.SharedLib.Models;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;
using WhalesExchangeBackend.Utils;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Controllers;

/// <summary>
/// Controller for REST API of the backend.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI.")]
internal class RestApiController : InternalControllerBase
{
    /// <summary>Number of satoshis that are considered high enough to justify waiting for two confirmations instead of one.</summary>
    private const long TwoConfirmationAmountThresholdSats = 1_000_000;

    /// <summary>Number of blocks to serve as a safe buffer before for a lockup address timeout scenario.</summary>
    /// <remarks>
    /// When the swap provider publishes the funding transaction with an output paying to the lockup address, the client must be provided with enough time to spend this output
    /// before the time lock on the output allows the swap provider to claim the money back. If the client learned about the the funding transaction just before the time lock
    /// expires, the client may fail to spend it on time.
    /// </remarks>
    private const int LockupAddressTimeoutBuffer = 5;

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log;

    /// <summary>Provides access to the current <see cref="HttpContext"/>.</summary>
    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>Service that checks client swap limits.</summary>
    private readonly SwapLimitChecker swapLimitChecker;

    /// <summary>Provider of access to swap providers and their offers in the database.</summary>
    private readonly SwapProviderRepository swapProviderRepository;

    /// <summary>Provider of access to swaps in the database.</summary>
    private readonly SwapRepository swapRepository;

    /// <summary>Client that communicates with Electrum RPC server.</summary>
    private readonly ElectrumRpcClient electrumRpcClient;

    /// <summary>Monitor of blockchain data fetched from the Electrum backend client.</summary>
    private readonly BlockchainDataMonitor blockchainDataMonitor;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current <see cref="HttpContext"/>.</param>
    /// <param name="swapLimitChecker">Service that checks client swap limits.</param>
    /// <param name="swapProviderRepository">Provider of access to swap providers and their offers in the database.</param>
    /// <param name="swapRepository">Provider of access to swaps in the database.</param>
    /// <param name="electrumRpcClient">Client that communicates with Electrum RPC server.</param>
    /// <param name="blockchainDataMonitor">Monitor of blockchain data fetched from the Electrum backend client.</param>
    public RestApiController(IHttpContextAccessor httpContextAccessor, SwapLimitChecker swapLimitChecker, SwapProviderRepository swapProviderRepository,
        SwapRepository swapRepository, ElectrumRpcClient electrumRpcClient, BlockchainDataMonitor blockchainDataMonitor)
    {
        this.httpContextAccessor = httpContextAccessor;
        HttpContext? context = this.httpContextAccessor.HttpContext;
        if (context is null)
            throw new SanityCheckException("HTTP context is null.");

        this.log = new(this.GetType().FullName!, $"{context.Connection.RemoteIpAddress}");

        this.swapLimitChecker = swapLimitChecker;
        this.swapProviderRepository = swapProviderRepository;
        this.swapRepository = swapRepository;
        this.electrumRpcClient = electrumRpcClient;
        this.blockchainDataMonitor = blockchainDataMonitor;

        this.log.Debug("*$");
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

        IPAddress? ipAddress = context.Connection.RemoteIpAddress;
        if (ipAddress is null)
            throw new SanityCheckException("Remote IP address is null.");

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
            response = new($"Getting swap provider with pubkey '{providerPk}' failed.");
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
                        string frontendId = RandomStringGenerator.Generate(DbSwap.FrontendIdLength);
                        string userIpAddress = ipAddress.ToString();
                        bool isPermitted = this.swapLimitChecker.RegisterSwap(ipAddress: userIpAddress, frontendSwapId: frontendId);
                        if (isPermitted)
                        {
                            swap = await this.swapRepository.InsertReverseAsync(frontendId: frontendId, providerPubkey: providerPk, userIpAddress: userIpAddress,
                                amountToPaySats: request.InvoiceAmount.Value, amountToReceiveSats: request.ExpectedAmount, claimAddress: request.ClientAddress,
                                claimPublicKey: request.ClaimPublicKey)
                                .ConfigureAwait(false);

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

                            // Register the lockup address to be monitored by the blockchain data monitor. This will allow the client to be notified when the funding transaction is
                            // seen and when it gets confirmed. Note that the required amount here needs to include the fees for the on-chain transaction that will claim the funds.
                            this.blockchainDataMonitor.RegisterMonitoredAddress(swapId: swap.Id, frontendId: swap.FrontendId, electrumSwapData.LockupAddress,
                                amountSats: electrumSwapData.OnChainAmountSats, requiredConfirmations: requiredConfirmations, timeoutHeight: timeoutHeight, isLockupAddress: true);

                            await this.swapRepository.MarkSwapAcceptedAsync(id: swap.Id, electrumSwapData.LockupAddress, timeoutBlockHeight: electrumSwapData.Locktime)
                                .ConfigureAwait(false);
                        }
                        else response = new("Too many uncommitted swaps for the given IP address.");
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
            _ = this.swapLimitChecker.UnregisterSwap(swap.FrontendId);

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
    /// Action that is executed when a swap is requested to be deleted.
    /// </summary>
    /// <param name="request">Request to remove a swap.</param>
    /// <returns>Result of the action method.</returns>
    [HttpPost]
    [Route("delete-swap")]
    public async Task<IActionResult> RemoveSwapAsync([FromBody] RemoveSwapRequest request)
    {
        this.log.Debug($"* {nameof(request)}='{request}'");

        IActionResult result;

        RemoveSwapResponse response;
        try
        {
            bool removed = await this.swapRepository.RemoveAsync(request.Id, maximumStatus: SwapStatus.Accepted).ConfigureAwait(false);
            if (removed)
            {
                this.blockchainDataMonitor.UnregisterMonitoredAddressWithFrontendId(request.Id);

                _ = this.swapLimitChecker.UnregisterSwap(frontendSwapId: request.Id);
                response = new();
            }
            else response = new($"Could not delete swap with frontend ID '{request.Id}'.");
        }
        catch (Exception e)
        {
            this.log.Error($"Exception occurred while removing swap with frontend ID '{request.Id}': {e}");
            response = new($"Removing swap with frontend ID '{request.Id}' from the database failed. {e.Message}");
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

    /// <summary>
    /// Action that is executed when a transaction broadcasting is requested.
    /// </summary>
    /// <param name="request">Request to broadcast a transaction.</param>
    /// <returns>Result of the action method.</returns>
    [HttpPost]
    [Route("broadcasttransaction")]
    public async Task<IActionResult> BroadcastTransactionAsync([FromBody] BroadcastTransactionRequest request)
    {
        this.log.Debug($"* {nameof(request)}='{request}'");

        HttpContext? context = this.httpContextAccessor.HttpContext;
        if (context is null)
            throw new SanityCheckException("HTTP context is null.");

        IActionResult result;

        if (request.Currency == "BTC")
        {
            try
            {
                string transactionId = await this.electrumRpcClient.BroadcastAsync(request.TransactionHex, context.RequestAborted).ConfigureAwait(false);
                BroadcastTransactionResponse response = new(transactionId);
                result = this.Ok(response);
            }
            catch (Exception e)
            {
                this.log.Error($"Exception occurred while broadcasting transaction: {e}");
                result = this.BadRequest($"Broadcasting transaction failed. {e.Message}");
            }
        }
        else result = this.BadRequest("Only BTC transactions are supported.");

        this.log.Debug("$");
        return result;
    }
}