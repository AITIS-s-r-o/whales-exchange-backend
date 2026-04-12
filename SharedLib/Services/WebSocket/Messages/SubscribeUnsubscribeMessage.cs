using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Client's message to subscribe to updates for swaps on the server.
/// </summary>
/// <seealso cref="SubscriptionUpdateMessage"/>
internal class SubscribeUnsubscribeMessage : OperationMessageBase
{
    /// <summary>Name of the channel to subscribe.</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; }

    /// <summary>IDs of the swaps to subscribe for.</summary>
    [JsonPropertyName("args")]
    public string[] SwapIds { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="channel">Name of the channel to subscribe.</param>
    /// <param name="swapIds">IDs of the swaps to subscribe for.</param>
    /// <param name="subscribe"><c>true</c> to subscribe, <c>false</c> to unsubscribe.</param>
    [JsonConstructor]
    public SubscribeUnsubscribeMessage(string channel, string[] swapIds, bool subscribe) :
        base(operation: subscribe ? Constants.OperationSubscribe : Constants.OperationUnsubscribe)
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
            nameof(SubscribeUnsubscribeMessage),
            nameof(this.Operation), this.Operation,
            nameof(this.Channel), this.Channel,
            nameof(this.SwapIds), this.SwapIds.LogJoin()
        );
    }
}