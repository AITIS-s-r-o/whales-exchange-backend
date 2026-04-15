using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <inheritdoc cref="IClientConnectionHandlerFactory"/>
internal class ClientConnectionHandlerFactory : IClientConnectionHandlerFactory
{
    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Factory for creating <see cref="ProtocolMessageProcessor"/>s.</summary>
    private readonly IProtocolMessageProcessorFactory protocolMessageProcessorFactory;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="protocolMessageProcessorFactory">Factory for creating <see cref="ProtocolMessageProcessor"/>s.</param>
    public ClientConnectionHandlerFactory(IProtocolMessageProcessorFactory protocolMessageProcessorFactory)
    {
        this.log.Debug("*");

        this.protocolMessageProcessorFactory = protocolMessageProcessorFactory;

        this.log.Debug("$");
    }

    /// <inheritdoc cref="IClientConnectionHandlerFactory.Create(System.Net.WebSockets.WebSocket, string)"/>
    public ClientConnectionHandler Create(System.Net.WebSockets.WebSocket webSocket, string instanceName)
    {
        this.log.Debug($"* {nameof(instanceName)}='{instanceName}'");

        ProtocolMessageProcessor protocolMessageProcessor = this.protocolMessageProcessorFactory.Create(instanceName);
        ClientConnectionHandler result = new(webSocket, protocolMessageProcessor, instanceName);

        this.log.Debug($"$='{result}'");
        return result;
    }
}