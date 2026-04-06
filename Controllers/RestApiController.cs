using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhalesExchangeBackend.Controllers.InternalSupport;
using WhalesExchangeBackend.Data;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Exceptions;
using WhalesExchangeBackend.Models;
using WhalesExchangeBackend.Services;
using WhalesExchangeBackend.Services.ElectrumRpc;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Controllers;

/// <summary>
/// Controller for REST API of the backend.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI.")]
internal class RestApiController : InternalControllerBase
{
    /// <summary>String identifier of the forward swap type.</summary>
    private const string ForwardSwapTypeStr = "submarine";

    /// <summary>String identifier of the order side for forward swaps.</summary>
    private const string ForwardSwapOrderSideStr = "sell";

    /// <summary>String identifier of the reverse swap type.</summary>
    private const string ReverseSwapTypeStr = "reversesubmarine";

    /// <summary>String identifier of the order side for reverse swaps.</summary>
    private const string ReverseSwapOrderSideStr = "buy";

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

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current <see cref="HttpContext"/>.</param>
    /// <param name="swapProviderRepository">Provider of access to swap providers and their offers in the database.</param>
    /// <param name="swapRepository">Provider of access to swaps in the database.</param>
    /// <param name="electrumRpcClient">Client that communicates with Electrum RPC server.</param>
    public RestApiController(IHttpContextAccessor httpContextAccessor, SwapProviderRepository swapProviderRepository, SwapRepository swapRepository,
        ElectrumRpcClient electrumRpcClient)
    {
        this.httpContextAccessor = httpContextAccessor;
        HttpContext? context = this.httpContextAccessor.HttpContext;
        if (context is null)
            throw new SanityCheckException("HTTP context is null.");

        this.log = new(this.GetType().FullName!, $"{context.Connection.RemoteIpAddress}");

        this.swapProviderRepository = swapProviderRepository;
        this.swapRepository = swapRepository;
        this.electrumRpcClient = electrumRpcClient;

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
        DateTime hourAgo = DateTime.UtcNow.AddHours(-1);

        GetSwapProvidersResponse response;
        try
        {
            DbSwapProvider[] dbRecords = await this.swapProviderRepository.GetRecentAsync(hourAgo).ConfigureAwait(false);

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
    /// <param name="type">Either <see cref="ForwardSwapTypeStr"/> or <see cref="ReverseSwapTypeStr"/>.</param>
    /// <param name="pairId">ID of the assets being swapped. This should be set to <c>BTC/BTC</c>.</param>
    /// <param name="orderSide"><see cref="ForwardSwapOrderSideStr"/> if <paramref name="type"/> is <see cref="ForwardSwapTypeStr"/>, <see cref="ReverseSwapOrderSideStr"/> if it is
    /// <see cref="ReverseSwapOrderSideStr"/>.</param>
    /// <param name="invoiceAmount">Amount the user has to pay in satoshis for reverse swaps, or <c>null</c> for forward swaps.</param>
    /// <param name="invoice">Lightning invoice for forward swaps, or <c>null</c> for reverse swaps.</param>
    /// <param name="expectedAmount">Amount the user expects to receive in satoshis.</param>
    /// <param name="preimageHash">Hash of the preimage.</param>
    /// <param name="claimPublicKey">Public key that will be used to claim the on-chain funds, or <c>null</c> for forward swaps.</param>
    /// <param name="refundPublicKey">Public key that will be used for the refund of the on-chain funds if a forward swap fails, or <c>null</c> for reverse swaps.</param>
    /// <param name="pairHash">Public key of the selected swap provider.</param>
    /// <returns>Result of the action method.</returns>
    [HttpPost]
    [Route("/createswap")]
    public async Task<IActionResult> CreateSwapAsync(string type, string pairId, string orderSide, long? invoiceAmount, string? invoice, long expectedAmount, string preimageHash,
        string? claimPublicKey, string? refundPublicKey, string pairHash)
    {
        this.log.Debug($"* {nameof(type)}='{type}',{nameof(pairId)}='{pairId}',{orderSide}='{orderSide}',{nameof(invoiceAmount)}={invoiceAmount},{nameof(invoice)}='{invoice}',{
            nameof(expectedAmount)}={expectedAmount},{nameof(preimageHash)}='{preimageHash}',{nameof(pairHash)}='{pairHash}',{nameof(claimPublicKey)}='{claimPublicKey}',{
            nameof(refundPublicKey)}='{refundPublicKey}'");

        IActionResult result;
        CreateSwapResponse response;

        string providerPk = pairHash;
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

        HttpContext? context = this.httpContextAccessor.HttpContext;
        if (context is null)
            throw new SanityCheckException("HTTP context is null.");

        long? swapId = null;
        bool failed = false;
        try
        {
            if ((type == ForwardSwapTypeStr) && (orderSide == ForwardSwapOrderSideStr))
            {
                if (invoice is not null)
                {
                    if (refundPublicKey is not null)
                    {
                        response = new("Forward swaps are not supported at the moment.");
                    }
                    else response = new($"'{nameof(refundPublicKey)}' is mandatory for forward swaps.");
                }
                else response = new($"'{nameof(invoice)}' is mandatory for forward swaps.");
            }
            else if ((type == ReverseSwapTypeStr) && (orderSide == ReverseSwapOrderSideStr))
            {
                if (invoiceAmount is not null)
                {
                    if (claimPublicKey is not null)
                    {
                        swapId = await this.swapRepository.InsertReverseAsync(providerPubkey: providerPk, amountToPaySats: invoiceAmount.Value,
                            amountToReceiveSats: expectedAmount).ConfigureAwait(false);

                        long prepayment = 2 * provider.MiningFeeReverseSat;
                        ElectrumSwapData electrumSwapData = await this.electrumRpcClient.ReverseSwapAsync(lnAmountSats: invoiceAmount.Value, onChainAmountSats: expectedAmount,
                            prepaymentSats: prepayment, preimageHash: preimageHash, claimPk: claimPublicKey, providerPk: providerPk, context.RequestAborted).ConfigureAwait(false);

                        SwapResponse swapResponse = new(reverse: true, asset: "BTC", invoice: electrumSwapData.Invoice, feeInvoice: electrumSwapData.FeeInvoice,
                            timeoutBlockHeight: electrumSwapData.Locktime, sendAmountSats: electrumSwapData.LightningAmountSats,
                            receiveAmountSats: electrumSwapData.OnChainAmountSats, redeemScript: electrumSwapData.RedeemScriptHex, lockupAddress: electrumSwapData.LockupAddress);

                        response = new(swapResponse);

                        await this.swapRepository.MarkSwapAcceptedAsync(id: swapId.Value, electrumSwapData.LockupAddress, timeoutBlockHeight: electrumSwapData.Locktime)
                            .ConfigureAwait(false);
                    }
                    else response = new($"'{nameof(claimPublicKey)}' is mandatory for reverse swaps.");
                }
                else response = new($"'{nameof(invoiceAmount)}' is mandatory for reverse swaps.");
            }
            else response = new($"Unknown swap type '{type}' and order side '{orderSide}' combination.");
        }
        catch (Exception e)
        {
            this.log.Error($"Exception occurred while creating a new swap: {e}");
            response = new($"Creating new swap failed. {e.Message}");
            failed = true;
        }

        if (failed && (swapId is not null))
        {
            await this.swapRepository.MarkSwapRejectedAsync(swapId.Value).ConfigureAwait(false);
        }

        result = this.Ok(response);

        this.log.Debug("$");
        return result;
    }
}