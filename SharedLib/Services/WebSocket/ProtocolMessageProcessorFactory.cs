using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <inheritdoc cref="IProtocolMessageProcessorFactory"/>
internal class ProtocolMessageProcessorFactory : IProtocolMessageProcessorFactory
{
    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Manager of swap subscriptions.</summary>
    private readonly SubscriptionManager subscriptionManager;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="subscriptionManager">Manager of swap subscriptions.</param>
    public ProtocolMessageProcessorFactory(SubscriptionManager subscriptionManager)
    {
        this.log.Debug("*");

        this.subscriptionManager = subscriptionManager;

        this.log.Debug("$");
    }

    /// <inheritdoc cref="IProtocolMessageProcessorFactory.Create(string)"/>/>
    public ProtocolMessageProcessor Create(string instanceName)
    {
        this.log.Debug($"* {nameof(instanceName)}='{instanceName}'");

        ProtocolMessageProcessor result = new(this.subscriptionManager, instanceName);

        this.log.Debug($"$='{result}'");
        return result;
    }
}