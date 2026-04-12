using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;
using WhalesSecret.TradeScriptLib.Exceptions;
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

            case SubscribeUnsubscribeMessage subscribeUnsubscribeMessage:
                if ((subscribeUnsubscribeMessage.Operation == Constants.OperationSubscribe) || (subscribeUnsubscribeMessage.Operation == Constants.OperationUnsubscribe))
                {
                    bool success = await this.ProcessSubscribeUnsubscribeMessageAsync(subscribeUnsubscribeMessage, clientConnectionHandler, cancellationToken)
                        .ConfigureAwait(false);
                    if (!success)
                    {
                        closingStatus = WebSocketCloseStatus.PolicyViolation;
                        closingDescription = "Processing subscribe/unsubscribe message failed.";
                    }
                }
                else
                {
                    closingStatus = WebSocketCloseStatus.InvalidMessageType;
                    closingDescription = $"Invalid operation '{subscribeUnsubscribeMessage.Operation}' received.";
                }

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
    /// Processes incoming <see cref="SubscribeUnsubscribeMessage"/>.
    /// </summary>
    /// <param name="message">Message to process.</param>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns><c>true</c> if the message was processed successfully, <c>false</c> if the client should be disconnected.</returns>
    private async Task<bool> ProcessSubscribeUnsubscribeMessageAsync(SubscribeUnsubscribeMessage message, ClientConnectionHandler clientConnectionHandler,
        CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(message)}='{message}'");

        bool result;
        if (message.Operation == Constants.OperationSubscribe)
        {
            result = await this.subscriptionManager.SubscribeAsync(message.SwapIds, clientConnectionHandler, cancellationToken).ConfigureAwait(false);
        }
        else if (message.Operation == Constants.OperationUnsubscribe)
        {
            this.subscriptionManager.Unsubscribe(message.SwapIds, clientConnectionHandler);
            result = true;
        }
        else throw new SanityCheckException($"Invalid operation '{message.Operation}' received.");

        this.log.Debug($"$='{result}'");
        return result;
    }
}