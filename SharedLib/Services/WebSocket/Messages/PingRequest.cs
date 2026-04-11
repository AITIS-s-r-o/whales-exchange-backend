using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Message to ping the other party.
/// </summary>
/// <seealso cref="PingResponse"/>
internal class PingRequest : RequestMessageBase
{
    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    [JsonConstructor]
    public PingRequest() :
        base(operation: "ping")
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0},{1}=`{2}`]",
            nameof(PingRequest),
            nameof(this.Operation), this.Operation
        );
    }
}