using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;
using WhalesExchangeBackend.SharedLib.Utils.Sync;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <summary>
/// Handler of client connections to the WebSocket endpoint.
/// </summary>
internal class ClientConnectionHandler : IAsyncDisposable
{
    /// <summary>Instance logger.</summary>
    private readonly WsLogger log;

    /// <summary>Connected WebSocket representing the incoming client.</summary>
    private readonly System.Net.WebSockets.WebSocket webSocket;

    /// <summary>Processor of WebSocket protocol messages.</summary>
    private readonly IProtocolMessageProcessor protocolMessageProcessor;

    /// <summary>Name of the instance for logging purposes only.</summary>
    private readonly string instanceName;

    /// <summary>Serialization options to use for serialization of messages.</summary>
    private readonly JsonSerializerOptions serializationOptions;

    /// <summary>Cancellation token source for asynchronous tasks that is being triggered when the component shutdown is initiated.</summary>
    private readonly CancellationTokenSource shutdownCancellationTokenSource;

    /// <summary>Cancellation token of <see cref="shutdownCancellationTokenSource"/>.</summary>
    private readonly CancellationToken shutdownCancellationToken;

    /// <summary>Lock to prevent concurrent writes to the WebSocket.</summary>
    private readonly AsyncLock writeLock;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="webSocket">Connected WebSocket representing the incoming client.</param>
    /// <param name="protocolMessageProcessor">Processor of WebSocket protocol messages.</param>
    /// <param name="instanceName">Name of the instance for logging purposes only.</param>
    public ClientConnectionHandler(System.Net.WebSockets.WebSocket webSocket, IProtocolMessageProcessor protocolMessageProcessor, string instanceName)
    {
        this.instanceName = instanceName;
        this.log = new(this.GetType().FullName!, this.instanceName);

        this.log.Debug("*");

        this.writeLock = new(this.instanceName);
        this.disposedValueLock = new();

        this.webSocket = webSocket;
        this.protocolMessageProcessor = protocolMessageProcessor;
        this.serializationOptions = new()
        {
            AllowTrailingCommas = true,
            WriteIndented = false,
        };

        this.shutdownCancellationTokenSource = new();
        this.shutdownCancellationToken = this.shutdownCancellationTokenSource.Token;

        this.log.Debug("$");
    }

    /// <summary>
    /// Handles the connected client.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RunClientHandlingLoopAsync(CancellationToken cancellationToken)
    {
        using IDisposable mdlc = this.log.SetMdlc();
        this.log.Debug("*");

        try
        {
            int offset = 0;
            byte[] buffer = new byte[Constants.MaxMessageSizeBytes];
            while (true)
            {
                WebSocketReceiveResult receiveResult;
                do
                {
                    this.log.Trace("Wait to receive next message.");

                    int readCount = buffer.Length - offset;

                    if (readCount <= 0)
                    {
                        this.log.Warn("Our byte buffer is too small to receive complete message. Aborting connection.");

                        this.webSocket.Abort();

                        this.log.Debug("$<BUFFER_TOO_SMALL>");
                        return;
                    }

                    ArraySegment<byte> segment = new(buffer, offset: offset, count: readCount);
                    try
                    {
                        receiveResult = await this.webSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        this.log.Debug($"Reading from the websocket failed. {e.Message}");
                        break;
                    }

                    if (receiveResult.CloseStatus is not null)
                    {
                        this.log.Debug($"Connection has been closed with status {receiveResult.CloseStatus.Value} and description '{receiveResult.CloseStatusDescription}'.");
                        break;
                    }

                    // The protocol is based on JSON messages exchanged using the text WebSocket messages.
                    if (receiveResult.MessageType != Constants.MessageType)
                    {
                        this.log.Debug("Invalid message received, closing connection.");
                        _ = await this.CloseAsync(WebSocketCloseStatus.ProtocolError, "Invalid message received.", cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    this.log.Trace($"Received message type {receiveResult.MessageType}, {receiveResult.Count} bytes long.");

                    offset += receiveResult.Count;
                }
                while (!receiveResult.EndOfMessage);

                if (offset == 0)
                    break;

                IWebSocketMessage? message = null;
                ArraySegment<byte> receivedSegment = new(buffer, offset: 0, count: offset);
                try
                {
                    message = JsonSerializer.Deserialize<IWebSocketMessage>(receivedSegment, this.serializationOptions);
                    if (message is null)
                        this.log.Warn("Received null message.");
                }
                catch (Exception e)
                {
                    string jsonString = System.Text.Encoding.UTF8.GetString(receivedSegment);
                    this.log.Warn($"Exception occurred while deserializing incoming message '{jsonString.ToBoundedString()}': {e}");
                }

                if (message is null)
                {
                    this.log.Debug("Invalid message received, closing connection.");
                    _ = await this.CloseAsync(WebSocketCloseStatus.ProtocolError, "Invalid message received.", cancellationToken).ConfigureAwait(false);
                    break;
                }

                if (message is OperationMessageBase requestMessage)
                {
                    ProcessedMessageResponse response = await this.protocolMessageProcessor.ProcessMessageAsync(requestMessage, clientConnectionHandler: this, cancellationToken)
                        .ConfigureAwait(false);

                    if (response.ResponseMessage is not null)
                    {
                        this.log.Debug($"Sending response message '{response.ResponseMessage.ToBoundedString()}' to the client.");

                        // Serialization should never fail.
                        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes<IWebSocketMessage>(response.ResponseMessage, this.serializationOptions);
                        bool sendResult = await this.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
                        if (!sendResult)
                        {
                            this.log.Debug($"Sending response message '{response.ResponseMessage}' failed.");
                            break;
                        }
                    }
                    else if (response.ClosingStatus is not null)
                    {
                        this.log.Debug($"Closing connection to the client with status {response.ClosingStatus}, closing description '{response.ClosingDescription}'.");

                        _ = await this.CloseAsync(response.ClosingStatus.Value, response.ClosingDescription, cancellationToken).ConfigureAwait(false);
                        break;
                    }
                }
                else if (message is EventMessageBase responseMessage)
                {
                    throw new SanityCheckException($"Unexpected response message '{message.GetType().FullName}' received.");
                }
                else throw new SanityCheckException($"Unsupported message type '{message.GetType().FullName}' received.");

                offset = 0;
            }
        }
        catch (OperationCanceledException)
        {
            this.log.Debug("Shutdown detected.");
        }
        catch (Exception e)
        {
            this.log.Error($"Exception occurred while handling the client: {e}");
            throw;
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Sends a swap subscription update message to the client.
    /// </summary>
    /// <param name="swapUpdates">List of swap updates to send.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns><c>true</c> if the operation succeeded, <c>false</c> otherwise.</returns>
    public async Task<bool> SendSwapUpdateAsync(SwapUpdate[] swapUpdates, CancellationToken cancellationToken)
    {
        this.log.Debug($"* |{nameof(swapUpdates)}|={swapUpdates.Length}");

        bool result = false;
        long timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SubscriptionUpdateMessage message = new(channel: Constants.SwapUpdatesChannel, timestampMs, swapUpdates);

        try
        {
            result = await this.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            this.log.Debug("Operation was cancelled.");
        }

        this.log.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Sends a WebSocket message to the client.
    /// </summary>
    /// <param name="message">Message to send.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <returns><c>true</c> if the function succeeded, <c>false</c> if the client is not connected or an error occurred while sending the message.</returns>
    private async Task<bool> SendMessageAsync(IWebSocketMessage message, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(message)}='{message}'");

        // Serialization should never fail.
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, this.serializationOptions);
        bool sendResult = await this.SendAsync(bytes, cancellationToken).ConfigureAwait(false);

        this.log.Debug($"$={sendResult}");
        return sendResult;
    }

    /// <summary>
    /// Low-level method for sending data to the WebSocket server.
    /// </summary>
    /// <param name="data">Bytes to send to the other party in the WebSocket format.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <returns><c>true</c> if the function succeeded, <c>false</c> if the client is not connected or an error occurred while sending the message.</returns>
    private async Task<bool> SendAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
    {
        this.log.Debug($"* |{nameof(data)}|={data.Count}");

        bool result = false;

        try
        {
            using IDisposable lockReleaser = await this.writeLock.EnterAsync().ConfigureAwait(false);
            await this.webSocket.SendAsync(data, Constants.MessageType, endOfMessage: true, cancellationToken).ConfigureAwait(false);
            result = true;
        }
        catch (OperationCanceledException)
        {
            this.log.Debug("Operation was cancelled.");
            throw;
        }
        catch (Exception e)
        {
            // This involves two cases: The client is not connected or the client was disposed (should never happen).
            this.log.Error($"Sending message to the counterparty failed with exception: {e}");
        }

        this.log.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Closes the client's WebSocket.
    /// </summary>
    /// <param name="closeStatus">Status with which to close the connection.</param>
    /// <param name="closeDescription">Human readable explanation of why the connection was closed.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns><c>true</c> if the function succeeded, <c>false</c> if the operation was cancelled.</returns>
    public async Task<bool> CloseAsync(WebSocketCloseStatus closeStatus, string? closeDescription, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(closeStatus)}={closeStatus},{nameof(closeDescription)}='{closeDescription}'");

        bool result = false;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.shutdownCancellationToken);
        using IDisposable lockReleaser = await this.writeLock.EnterAsync().ConfigureAwait(false);
        try
        {
            await this.webSocket.CloseAsync(closeStatus, closeDescription, linkedCts.Token).ConfigureAwait(false);
            result = true;
        }
        catch (OperationCanceledException)
        {
            this.log.Debug("Operation was cancelled.");
        }

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <inheritdoc/>
    public override string ToString()
        => this.instanceName;

    /// <summary>
    /// Frees managed resources used by the object.
    /// </summary>
    /// <returns>A <see cref="ValueTask">task</see> that represents the asynchronous dispose operation.</returns>
    protected virtual async ValueTask DisposeCoreAsync()
    {
        this.log.Debug("*");

        lock (this.disposedValueLock)
        {
            if (this.disposedValue)
            {
                this.log.Debug("$<ALREADY_DISPOSED>");
                return;
            }

            this.disposedValue = true;
        }

        this.log.Debug("Signaling shutdown.");
        await this.shutdownCancellationTokenSource.CancelAsync().ConfigureAwait(false);

        this.log.Debug("Dispose write limiter.");
        await this.writeLock.DisposeAsync().ConfigureAwait(false);

        this.log.Debug("Disposing shutdown token source.");
        this.shutdownCancellationTokenSource.Dispose();

        this.log.Debug("$");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}