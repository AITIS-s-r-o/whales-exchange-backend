using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WhalesExchangeBackend.Exceptions;
using WhalesExchangeBackend.Services.ElectrumRpc;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Client that communicates with Electrum RPC server.
/// </summary>
internal class ElectrumRpcClient
{
    /// <summary>Default query time for <c>get_submarine_swap_providers</c> call in seconds.</summary>
    private const int DefaultGetSubmarineSwapProvidersQueryTimeSeconds = 15;

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Server configuration helper.</summary>
    private readonly ConfigHelper configHelper;

    /// <summary>HTTP client used for communication with Electrum RPC.</summary>
    private readonly HttpClient httpClient;

    /// <summary>JSON options for (de)serialization of message for/from Electrum RPC server.</summary>
    private readonly JsonSerializerOptions jsonOptions;

    /// <summary>Authentication header to send to the Electrum RPC server.</summary>
    private readonly AuthenticationHeaderValue authHeader;

    /// <summary>ID of the last request message.</summary>
    private long lastRequestId;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="configHelper">Server configuration helper.</param>
    /// <param name="httpClient">HTTP client used for communication with Electrum RPC.</param>
    public ElectrumRpcClient(ConfigHelper configHelper, HttpClient httpClient)
    {
        this.log.Debug("*");

        this.configHelper = configHelper;
        this.httpClient = httpClient;

        this.jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        byte[] credentialBytes = Encoding.ASCII.GetBytes($"{this.configHelper.ElectrumRpcConfig.User}:{this.configHelper.ElectrumRpcConfig.Pass}");
        string base64Auth = Convert.ToBase64String(credentialBytes);
        this.authHeader = new("Basic", base64Auth);

        this.log.Debug("$");
    }

    /// <summary>
    /// Calls an Electrum RPC method with parameters.
    /// </summary>
    /// <typeparam name="TResult">Expected result type.</typeparam>
    /// <param name="method">RPC method name.</param>
    /// <param name="parameters">Array of parameters.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>Electrum RPC response.</returns>
    /// <exception cref="ElectrumRpcException ">Thrown when the Electrum server responded with an error.</exception>
    /// <exception cref="OperationFailedException">Thrown when the operation failed except for error returned by the Electrum server.</exception>
    private async Task<TResult> CallAsync<TResult>(string method, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(method)}='{method}',|{nameof(parameters)}|={parameters?.Count}");

        long id = Interlocked.Increment(ref this.lastRequestId);

        object @params = Array.Empty<object>();
        if (parameters is not null)
            @params = parameters;

        ElectrumRpcRequest request = new(id, method, @params);

        TResult result;
        try
        {
            string json = JsonSerializer.Serialize(request, this.jsonOptions);
            using StringContent content = new(json, Encoding.UTF8, "application/json");

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, this.configHelper.ElectrumRpcConfig.Uri)
            {
                Content = content,
            };

            httpRequest.Headers.Authorization = this.authHeader;

            using HttpResponseMessage response = await this.httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ElectrumRpcResponse<TResult>? rpcResponse = JsonSerializer.Deserialize<ElectrumRpcResponse<TResult>>(responseJson, this.jsonOptions);

            if ((rpcResponse is null) || (rpcResponse.Result is null))
                throw new OperationFailedException($"Calling Electrum RPC method '{method}' produced null response.");

            if (rpcResponse.Error is not null)
                throw new ElectrumRpcException(rpcResponse.Error.Code, rpcResponse.Error.Message, rpcResponse.Error.Data);

            result = rpcResponse.Result;
        }
        catch (OperationFailedException)
        {
            throw;
        }
        catch (ElectrumRpcException e)
        {
            this.log.Debug($"JSON RPC requested for method '{method}' failed with exception: {e}");
            throw;
        }
        catch (Exception e)
        {
            this.log.Debug("$<EXCEPTION>");
            throw new OperationFailedException($"Calling Electrum RPC method '{method}' failed.", e);
        }

        this.log.Debug($"$={result}");
        return result;
    }

    /// <inheritdoc cref="GetSubmarineSwapProvidersAsync(int, CancellationToken)"/>
    public Task<ElectrumSwapProvider[]> GetSubmarineSwapProvidersAsync(CancellationToken cancellationToken)
        => this.GetSubmarineSwapProvidersAsync(queryTimeSec: DefaultGetSubmarineSwapProvidersQueryTimeSeconds, cancellationToken);

    /// <summary>
    /// Calls Electrum's <c>get_submarine_swap_providers</c> RPC method.
    /// </summary>
    /// <param name="queryTimeSec">Timeout for how long the relays should be queried for provider announcements.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>List of electrum swap providers.</returns>
    /// <exception cref="ElectrumRpcException ">Thrown when the Electrum server responded with an error.</exception>
    /// <exception cref="OperationFailedException">Thrown when the operation failed except for error returned by the Electrum server.</exception>
    public async Task<ElectrumSwapProvider[]> GetSubmarineSwapProvidersAsync(int queryTimeSec, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(queryTimeSec)}={queryTimeSec}");

        Dictionary<string, object> parameters = new()
        {
            { "query_time", queryTimeSec },
        };

        ElectrumGetSubmarineSwapProviderResponse response = await this.CallAsync<ElectrumGetSubmarineSwapProviderResponse>(method: "get_submarine_swap_providers", parameters,
            cancellationToken).ConfigureAwait(false);

        ElectrumSwapProvider[] result = response.Values.ToArray();

        this.log.Debug($"|$|={result.Length}");
        return result;
    }
}