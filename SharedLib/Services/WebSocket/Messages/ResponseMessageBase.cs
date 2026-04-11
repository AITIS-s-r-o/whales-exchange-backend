using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Base class for response messages.
/// </summary>
internal abstract class ResponseMessageBase : ISignalsProtocolMessage
{
    /// <summary>Error indicating that the response has an invalid type.</summary>
    public const string ErrorInvalidTypeResponse = nameof(ResponseMessageBase) + "_" + nameof(ErrorInvalidTypeResponse);

    /// <summary>Type of the event.</summary>
    [JsonPropertyName("event")]
    public string Event { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="event">Type of the event.</param>
    [JsonConstructor]
    protected ResponseMessageBase(string @event)
    {
        this.Event = @event;
    }
}