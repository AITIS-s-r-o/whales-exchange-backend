using System.Globalization;
using System.Net.WebSockets;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Result of processing a protocol message.
/// </summary>
internal class ProcessedMessageResponse
{
    /// <summary>Status with which the connection should be closed, or <c>null</c> if the connection should be kept alive.</summary>
    public WebSocketCloseStatus? ClosingStatus { get; }

    /// <summary>Description with which the connection should be closed, or <c>null</c> if the connection should be kept alive.</summary>
    public string? ClosingDescription { get; }

    /// <summary>Response to send to the client, or <c>null</c> if the connection should be closed or if no response should be sent to the client.</summary>
    public EventMessageBase? ResponseMessage { get; }

    /// <summary>
    /// Creates a new instance of the object to close the connection.
    /// </summary>
    /// <param name="closingStatus">Status with which the connection should be closed.</param>
    /// <param name="closingDescription">Description with which the connection should be closed.</param>
    public ProcessedMessageResponse(WebSocketCloseStatus closingStatus, string closingDescription)
    {
        this.ClosingStatus = closingStatus;
        this.ClosingDescription = closingDescription;
    }

    /// <summary>
    /// Creates a new instance of the object to send a response message to the client.
    /// </summary>
    /// <param name="responseMessage">Response to send to the client, or <c>null</c> if no response should be sent to the client.</param>
    public ProcessedMessageResponse(EventMessageBase? responseMessage)
    {
        this.ResponseMessage = responseMessage;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`,{4}=`{5}`]",
            nameof(this.ClosingStatus), this.ClosingStatus,
            nameof(this.ClosingDescription), this.ClosingDescription,
            nameof(this.ResponseMessage), this.ResponseMessage
        );
    }
}