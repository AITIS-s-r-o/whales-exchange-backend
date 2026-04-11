using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Server's response to client's <see cref="PingRequest"/>.
/// </summary>
/// <seealso cref="PingRequest"/>
internal class PingResponse : ResponseMessageBase
{
    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    [JsonConstructor]
    public PingResponse() :
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
            nameof(PingResponse),
            nameof(this.Event), this.Event
        );
    }
}