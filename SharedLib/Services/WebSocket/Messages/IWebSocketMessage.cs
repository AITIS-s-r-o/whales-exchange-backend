using System.Text.Json.Serialization;
using WhalesExchangeBackend.SharedLib.Encoding.Converters;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Interface denoting a message received from a WebSocket connection.
/// </summary>
[JsonConverter(typeof(WebSocketMessageConverter))]
internal interface IWebSocketMessage
{
}