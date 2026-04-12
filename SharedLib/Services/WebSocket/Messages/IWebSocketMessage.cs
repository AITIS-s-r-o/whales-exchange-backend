using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Interface denoting a message received from a trading signals WebSocket connection.
/// </summary>
[JsonDerivedType(typeof(PingMessage), typeDiscriminator: nameof(PingMessage))]
[JsonDerivedType(typeof(PongMessage), typeDiscriminator: nameof(PongMessage))]
internal interface IWebSocketMessage
{
}