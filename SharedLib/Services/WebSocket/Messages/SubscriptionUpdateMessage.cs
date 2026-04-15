using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Server's message to inform the client about updates for swaps the client subscribed for.
/// </summary>
/// <seealso cref="UnsubscribeMessage"/>
internal class SubscriptionUpdateMessage : EventMessageBase
{
    /// <summary>Name of the channel.</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; }

    /// <summary>UNIX timestamp of the event in milliseconds.</summary>
    [JsonPropertyName("timestamp")]
    public long TimestampMs { get; }

    /// <summary>List of updates for the swaps.</summary>
    [JsonPropertyName("args")]
    public SwapUpdate[] SwapUpdates { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="channel">Name of the channel to subscribe.</param>
    /// <param name="timestampMs">UNIX timestamp of the event in milliseconds.</param>
    /// <param name="swapUpdates">List of updates for the swaps.</param>
    [JsonConstructor]
    public SubscriptionUpdateMessage(string channel, long timestampMs, SwapUpdate[] swapUpdates) :
        base(@event: Constants.EventSubscriptionUpdate)
    {
        this.Channel = channel;
        this.TimestampMs = timestampMs;
        this.SwapUpdates = swapUpdates;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0},{1}=`{2}`,{3}=`{4}`,{5}={6},{7}={8}]",
            nameof(SubscriptionUpdateMessage),
            nameof(this.Event), this.Event,
            nameof(this.Channel), this.Channel,
            nameof(this.TimestampMs), this.TimestampMs,
            nameof(this.SwapUpdates), this.SwapUpdates.LogJoin()
        );
    }
}