using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Client's message to subscribe to updates for swaps on the server.
/// </summary>
/// <seealso cref="UnsubscribeMessage"/>
/// <seealso cref="SubscriptionUpdateMessage"/>
internal class SubscribeMessage : OperationMessageBase
{
    /// <summary>Name of the channel to subscribe.</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; }

    /// <summary>IDs of the swaps to subscribe.</summary>
    [JsonPropertyName("args")]
    public string[] SwapIds { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="channel">Name of the channel to subscribe.</param>
    /// <param name="swapIds">IDs of the swaps to subscribe.</param>
    [JsonConstructor]
    public SubscribeMessage(string channel, string[] swapIds) :
        base(operation: Constants.OperationSubscribe)
    {
        this.Channel = channel;
        this.SwapIds = swapIds;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0},{1}=`{2}`,{3}=`{4}`,{5}={6}]",
            nameof(SubscribeMessage),
            nameof(this.Operation), this.Operation,
            nameof(this.Channel), this.Channel,
            nameof(this.SwapIds), this.SwapIds.LogJoin()
        );
    }
}