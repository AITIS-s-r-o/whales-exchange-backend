using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Base class for request messages.
/// </summary>
internal abstract class RequestMessageBase : ISignalsProtocolMessage
{
    /// <summary>Operation type.</summary>
    [JsonPropertyName("op")]
    public string Operation { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="operation">Operation type.</param>
    [JsonConstructor]
    protected RequestMessageBase(string operation)
    {
        this.Operation = operation;
    }
}