using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// RPC request for Electrum RPC server.
/// </summary>
internal class ElectrumRpcRequest
{
    /// <summary>JSON RPC version 2.</summary>
    private const string JsonRpcVersion = "2.0";

    /// <summary>JSON RPC version.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; }

    /// <summary>Identifier of the request.</summary>
    [JsonPropertyName("id")]
    public long Id { get; }

    /// <summary>Name of the RPC method to call.</summary>
    [JsonPropertyName("method")]
    public string Method { get; }

    /// <summary>Parameters of the called method, or <c>null</c> if the request has no parameters.</summary>
    [JsonPropertyName("params")]
    public object? Params { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="id">Identifier of the request.</param>
    /// <param name="method">Name of the RPC method to call.</param>
    /// <param name="params">Parameters of the called method, or <c>null</c> if the request has no parameters.</param>
    public ElectrumRpcRequest(long id, string method, object? @params)
    {
        this.JsonRpc = JsonRpcVersion;
        this.Id = id;
        this.Method = method;
        this.Params = @params;
    }
}