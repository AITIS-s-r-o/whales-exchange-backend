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
    /// <param name="request">Swap request.</param>
    /// <returns>Result of the action method.</returns>
    [HttpPost]
    [Route("/createswap")]
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

        long? swapId = null;
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
                        swapId = await this.swapRepository.InsertReverseAsync(providerPubkey: providerPk, amountToPaySats: request.InvoiceAmount.Value,
                            amountToReceiveSats: request.ExpectedAmount).ConfigureAwait(false);

                        long prepaymentSats = 2 * provider.MiningFeeReverseSat;
                        ElectrumSwapData electrumSwapData = await this.electrumRpcClient.ReverseSwapAsync(lnAmountSats: request.InvoiceAmount.Value,
                            onChainAmountSats: request.ExpectedAmount, prepaymentSats: prepaymentSats, preimageHash: request.PreimageHash, claimPk: request.ClaimPublicKey,
                            providerPk: providerPk, context.RequestAborted).ConfigureAwait(false);

                        SwapResponse swapResponse = new(reverse: true, asset: "BTC", invoice: electrumSwapData.Invoice, feeInvoice: electrumSwapData.FeeInvoice,
                            timeoutBlockHeight: electrumSwapData.Locktime, sendAmountSats: electrumSwapData.LightningAmountSats,
                            receiveAmountSats: request.ExpectedAmount, onChainAmountSats: electrumSwapData.OnChainAmountSats, redeemScript: electrumSwapData.RedeemScriptHex,
                            lockupAddress: electrumSwapData.LockupAddress);

                        response = new(swapResponse);

                        await this.swapRepository.MarkSwapAcceptedAsync(id: swapId.Value, electrumSwapData.LockupAddress, timeoutBlockHeight: electrumSwapData.Locktime)
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

        if (failed && (swapId is not null))
        {
            try
            {
                await this.swapRepository.MarkSwapRejectedAsync(swapId.Value).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.log.Error($"Exception occurred while marking swap ID {swapId} as rejected: {e}");
            }
        }

        result = this.Ok(response);

        this.log.Debug("$");
        return result;
    }
}