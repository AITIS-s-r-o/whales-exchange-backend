using System.Globalization;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response for <see cref="RestApiController.GetSwapStatusAsync(string)"/>.
/// </summary>
internal class GetSwapStatusResponse
{
    /// <summary>Status of the swap, or <c>null</c> if error occurred.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; }

    /// <summary>Reason of the failure if the swap failed, or <c>null</c> if no failure is reported.</summary>
    /// <remarks>Note that unlike <see cref="Error"/>, this is an error message that is related to the failure of the swap, not the REST API call.</remarks>
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; }

    /// <summary>Description of a Bitcoin transaction associated with the swap update, or <c>null</c> if no transaction is associated.</summary>
    [JsonPropertyName("transaction")]
    public SwapStatusTransaction? Transaction { get; }

    /// <summary>Error message, or <c>null</c> if the call succeeded.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="status">Status of the swap, or <c>null</c> if error occurred.</param>
    /// <param name="failureReason">Reason of the failure if the swap failed, or <c>null</c> if no failure is reported.</param>
    /// <param name="transaction">Description of a Bitcoin transaction associated with the swap update, or <c>null</c> if no transaction is associated.</param>
    /// <param name="error">Error message, or <c>null</c> if the call succeeded.</param>
    public GetSwapStatusResponse(string? status, string? failureReason, SwapStatusTransaction? transaction, string? error)
    {
        this.Status = status;
        this.FailureReason = failureReason;
        this.Transaction = transaction;
        this.Error = error;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}=`{5}`,{6}=`{7}`]",
            nameof(this.Status), this.Status,
            nameof(this.FailureReason), this.FailureReason,
            nameof(this.Transaction), this.Transaction,
            nameof(this.Error), this.Error
        );
    }
}