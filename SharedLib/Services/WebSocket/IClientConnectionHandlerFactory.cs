namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <summary>
/// Factory for creating <see cref="ClientConnectionHandler"/>s.
/// </summary>
internal interface IClientConnectionHandlerFactory
{
    /// <summary>
    /// Creates a new <see cref="ClientConnectionHandler"/>.
    /// </summary>
    /// <param name="webSocket">Connected WebSocket representing the incoming client.</param>
    /// <param name="instanceName">Name of the instance for logging purposes only.</param>
    /// <returns>Newly created client connection handler.</returns>
    public ClientConnectionHandler Create(System.Net.WebSockets.WebSocket webSocket, string instanceName);
}