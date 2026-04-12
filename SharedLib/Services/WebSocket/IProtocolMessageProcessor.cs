using System.Threading;
using System.Threading.Tasks;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <summary>
/// Processor of WebSocket protocol messages.
/// </summary>
internal interface IProtocolMessageProcessor
{
    /// <summary>
    /// Processes a WebSocket protocol message.
    /// </summary>
    /// <param name="message">Message to process.</param>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>Response for the processed request.</returns>
    /// <remarks>Interruption of processing due to cancellation of <paramref name="cancellationToken"/> leads to a result to close the connection.</remarks>
    public Task<ProcessedMessageResponse> ProcessMessageAsync(OperationMessageBase message, ClientConnectionHandler clientConnectionHandler, CancellationToken cancellationToken);
}