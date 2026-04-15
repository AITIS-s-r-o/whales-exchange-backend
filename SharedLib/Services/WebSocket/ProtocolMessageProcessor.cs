using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <inheritdoc cref="IProtocolMessageProcessor"/>
internal class ProtocolMessageProcessor : IProtocolMessageProcessor
{
    /// <summary>Instance logger.</summary>
    private readonly WsLogger log;

    /// <summary>Manager of swap subscriptions.</summary>
    private readonly SubscriptionManager subscriptionManager;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="subscriptionManager">Manager of swap subscriptions.</param>
    /// <param name="instanceName">Name of the instance for logging purposes only.</param>
    public ProtocolMessageProcessor(SubscriptionManager subscriptionManager, string instanceName)
    {
        this.log = new(this.GetType().FullName!, instanceName);

        this.log.Debug("*");

        this.subscriptionManager = subscriptionManager;

        this.log.Debug("$");
    }

    /// <inheritdoc/>
    public async Task<ProcessedMessageResponse> ProcessMessageAsync(OperationMessageBase message, ClientConnectionHandler clientConnectionHandler,
        CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(message)}='{message}',{nameof(clientConnectionHandler)}='{clientConnectionHandler}'");

        WebSocketCloseStatus? closingStatus = null;
        string closingDescription = string.Empty;
        EventMessageBase? responseMessage = null;

        switch (message)
        {
            case PingMessage pingMessage:
                responseMessage = await this.ProcessPingMessageAsync(pingMessage).ConfigureAwait(false);
                break;

            case SubscribeMessage subscribeMessage:
                bool success = await this.ProcessSubscribeMessageAsync(subscribeMessage, clientConnectionHandler, cancellationToken).ConfigureAwait(false);
                if (!success)
                {
                    closingStatus = WebSocketCloseStatus.PolicyViolation;
                    closingDescription = "Processing subscribe/unsubscribe message failed.";
                }

                break;

            case UnsubscribeMessage unsubscribeMessage:
                this.ProcessUnsubscribeMessage(unsubscribeMessage, clientConnectionHandler);
                break;

            default:
                closingStatus = WebSocketCloseStatus.InvalidMessageType;
                closingDescription = $"Invalid message type '{message.GetType().FullName}'.";
                break;
        }

        ProcessedMessageResponse result = closingStatus is null ? new(responseMessage) : new(closingStatus.Value, closingDescription);

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// Processes incoming <see cref="PingMessage"/>.
    /// </summary>
    /// <param name="message">Message to process.</param>
    /// <returns>Response to the request.</returns>
    private async Task<PongMessage> ProcessPingMessageAsync(PingMessage message)
    {
        this.log.Debug($"* {nameof(message)}='{message}'");

        PongMessage result = new();

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// Processes incoming <see cref="SubscribeMessage"/>.
    /// </summary>
    /// <param name="message">Message to process.</param>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns><c>true</c> if the message was processed successfully, <c>false</c> if the client should be disconnected.</returns>
    private async Task<bool> ProcessSubscribeMessageAsync(SubscribeMessage message, ClientConnectionHandler clientConnectionHandler, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(message)}='{message}'");

        bool result = await this.subscriptionManager.SubscribeAsync(message.SwapIds, clientConnectionHandler, cancellationToken).ConfigureAwait(false);

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// Processes incoming <see cref="UnsubscribeMessage"/>.
    /// </summary>
    /// <param name="message">Message to process.</param>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint.</param>
    private void ProcessUnsubscribeMessage(UnsubscribeMessage message, ClientConnectionHandler clientConnectionHandler)
    {
        this.log.Debug($"* {nameof(message)}='{message}'");

        this.subscriptionManager.Unsubscribe(message.SwapIds, clientConnectionHandler);

        this.log.Debug("$");
    }
}