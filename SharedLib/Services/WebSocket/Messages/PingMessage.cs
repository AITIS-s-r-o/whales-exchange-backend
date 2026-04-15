using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Message to ping the other party.
/// </summary>
/// <seealso cref="PongMessage"/>
internal class PingMessage : OperationMessageBase
{
    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    [JsonConstructor]
    public PingMessage() :
        base(operation: Constants.OperationPing)
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0},{1}=`{2}`]",
            nameof(PingMessage),
            nameof(this.Operation), this.Operation
        );
    }
}