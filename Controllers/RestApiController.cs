using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhalesExchangeBackend.Controllers.InternalSupport;
using WhalesExchangeBackend.Data;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Models;
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

    /// <summary>Provider of access to swap providers and their offers in the database.</summary>
    private readonly SwapProviderRepository swapProviderRepository;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current <see cref="HttpContext"/>.</param>
    /// <param name="swapProviderRepository">Provider of access to swap providers and their offers in the database.</param>
    public RestApiController(IHttpContextAccessor httpContextAccessor, SwapProviderRepository swapProviderRepository)
    {
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is null)
            throw new SanityCheckException("HTTP context is null.");

        this.log = new(this.GetType().FullName!, $"{context.Connection.RemoteIpAddress}");

        this.swapProviderRepository = swapProviderRepository;

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
            response = new($"Getting list of swap providers from the databas failed. {e.Message}");
        }

        result = this.Ok(response);

        this.log.Debug("$");
        return result;
    }
}