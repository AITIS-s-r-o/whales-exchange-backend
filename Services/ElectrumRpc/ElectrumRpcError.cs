using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Error of the Electrum RPC response.
/// </summary>
internal class ElectrumRpcError
{
    /// <summary>Error code.</summary>
    [JsonPropertyName("code")]
    public int Code { get; }

    /// <summary>Error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; }

    /// <summary>Error details, or <c>null</c> if no details are provided.</summary>
    [JsonPropertyName("data")]
    public object? Data { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="data">Error details, or <c>null</c> if no details are provided.</param>
    public ElectrumRpcError(int code, string message, object? data)
    {
        this.Code = code;
        this.Message = message;
        this.Data = data;
    }
}