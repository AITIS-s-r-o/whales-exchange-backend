using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Server's response to client's <see cref="PingMessage"/>.
/// </summary>
/// <seealso cref="PingMessage"/>
internal class PongMessage : EventMessageBase
{
    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    [JsonConstructor]
    public PongMessage() :
        base(@event: "pong")
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0},{1}=`{2}`]",
            nameof(PongMessage),
            nameof(this.Event), this.Event
        );
    }
}