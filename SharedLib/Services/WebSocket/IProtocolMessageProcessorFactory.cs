namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <summary>
/// Factory for creating <see cref="ProtocolMessageProcessor"/>s.
/// </summary>
internal interface IProtocolMessageProcessorFactory
{
    /// <summary>
    /// Creates a new <see cref="ProtocolMessageProcessor"/>.
    /// </summary>
    /// <param name="instanceName">Name of the instance for logging purposes only.</param>
    /// <returns>Newly created protocol message processor.</returns>
    public ProtocolMessageProcessor Create(string instanceName);
}