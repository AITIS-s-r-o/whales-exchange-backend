using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.SharedLib.Services.WebSocket;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

namespace WhalesExchangeBackend.SharedLib.Encoding.Converters;

/// <summary>
/// JSON converter for <see cref="IWebSocketMessage"/>.
/// </summary>
internal class WebSocketMessageConverter : JsonConverter<IWebSocketMessage>
{
    /// <inheritdoc/>
    public override IWebSocketMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("op", out JsonElement operationElement))
        {
            string op = operationElement.GetString() ?? string.Empty;
            return op switch
            {
                Constants.OperationPing => JsonSerializer.Deserialize<PingMessage>(root.GetRawText(), options),
                Constants.OperationSubscribe => JsonSerializer.Deserialize<SubscribeMessage>(root.GetRawText(), options),
                Constants.OperationUnsubscribe => JsonSerializer.Deserialize<UnsubscribeMessage>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown operation '{op}'"),
            };
        }

        if (root.TryGetProperty("event", out JsonElement eventElement))
        {
            string eventName = eventElement.GetString() ?? string.Empty;
            return eventName switch
            {
                Constants.EventPong => JsonSerializer.Deserialize<PongMessage>(root.GetRawText(), options),
                Constants.EventSubscriptionUpdate => JsonSerializer.Deserialize<SubscriptionUpdateMessage>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown event '{eventName}'"),
            };
        }

        throw new JsonException("Missing discriminator property 'op' or 'event'.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IWebSocketMessage value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case PingMessage pingMessage:
                JsonSerializer.Serialize(writer, pingMessage, options);
                break;

            case PongMessage pongMessage:
                JsonSerializer.Serialize(writer, pongMessage, options);
                break;

            case SubscribeMessage subscribeMessage:
                JsonSerializer.Serialize(writer, subscribeMessage, options);
                break;

            case UnsubscribeMessage unsubscribeMessage:
                JsonSerializer.Serialize(writer, unsubscribeMessage, options);
                break;

            case SubscriptionUpdateMessage subscriptionUpdateMessage:
                JsonSerializer.Serialize(writer, subscriptionUpdateMessage, options);
                break;

            default:
                throw new JsonException($"Unsupported message type '{value.GetType().FullName}' provided.");
        }
    }
}