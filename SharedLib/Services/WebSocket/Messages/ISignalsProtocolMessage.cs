using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Interface denoting a message received from a trading signals WebSocket connection.
/// </summary>
[JsonDerivedType(typeof(PingRequest), typeDiscriminator: nameof(PingRequest))]
[JsonDerivedType(typeof(PingResponse), typeDiscriminator: nameof(PingResponse))]
internal interface ISignalsProtocolMessage
{
}