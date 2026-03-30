using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// RPC response from Electrum RPC server.
/// </summary>
/// <typeparam name="T">Type of the JSON RPC result.</typeparam>
internal class ElectrumRpcResponse<T>
{
    /// <summary>JSON RPC version.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; }

    /// <summary>Identifier of the request.</summary>
    [JsonPropertyName("id")]
    public int Id { get; }

    /// <summary>Result of the RPC call if the call succeeded, or <c>null</c> if the call failed.</summary>
    [JsonPropertyName("result")]
    public T? Result { get; }

    /// <summary>Description of the error of the call if the call failed, or <c>null</c> if the call succeeded.</summary>
    [JsonPropertyName("error")]
    public ElectrumRpcError? Error { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="jsonRpc">JSON RPC version.</param>
    /// <param name="id">Identifier of the request.</param>
    /// <param name="result">Result of the RPC call if the call succeeded, or <c>null</c> if the call failed.</param>
    /// <param name="error">Description of the error of the call if the call failed, or <c>null</c> if the call succeeded.</param>
    public ElectrumRpcResponse(string jsonRpc, int id, T? result, ElectrumRpcError? error)
    {
        this.JsonRpc = jsonRpc;
        this.Id = id;
        this.Result = result;
        this.Error = error;
    }
}