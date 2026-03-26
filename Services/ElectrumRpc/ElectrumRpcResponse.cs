using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// RPC response from Electrum RPC server.
/// </summary>
internal class ElectrumRpcResponse<T>
{
    /// <summary>JSON RPC version.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; }

    /// <summary>Identifier of the request.</summary>
    [JsonPropertyName("id")]
    public int Id { get; }

    /// <summary>Name of the RPC method to call.</summary>
    [JsonPropertyName("result")]
    public T? Result { get; }

    /// <summary>Parameters of the called method.</summary>
    [JsonPropertyName("error")]
    public ElectrumRpcError? Error { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="jsonRpc">JSON RPC version.</param>
    /// <param name="id">Identifier of the request.</param>
    /// <param name="result">Name of the RPC method to call.</param>
    /// <param name="error">Parameters of the called method.</param>
    public ElectrumRpcResponse(string jsonRpc, int id, T? result, ElectrumRpcError? error)
    {
        this.JsonRpc = jsonRpc;
        this.Id = id;
        this.Result = result;
        this.Error = error;
    }
}