using System.Globalization;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response for <see cref="RestApiController.GetSwapTransactionAsync(GetSwapTransactionRequest)"/>.
/// </summary>
internal class GetSwapTransactionResponse
{
    /// <summary>Funding Bitcoin transaction ID in hex format, or <c>null</c> if error occurred.</summary>
    [JsonPropertyName("id")]
    public string? TransactionId { get; }

    /// <summary>Funding Bitcoin transaction data in hex format, or <c>null</c> if error occurred or if not available.</summary>
    [JsonPropertyName("hex")]
    public string? TransactionData { get; }

    /// <summary>Block height at which the funding transaction expires, or <c>null</c> if error occurred or if not available.</summary>
    [JsonPropertyName("timeoutBlockHeight")]
    public long? TimeoutBlockHeight { get; }

    /// <summary>Error message, or <c>null</c> if the call succeeded.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="transactionId">Funding Bitcoin transaction ID in hex format, or <c>null</c> if error occurred.</param>
    /// <param name="transactionData">Funding Bitcoin transaction data in hex format, or <c>null</c> if error occurred or if not available.</param>
    /// <param name="timeoutBlockHeight">Block height at which the funding transaction expires, or <c>null</c> if error occurred or if not available.</param>
    /// <param name="error">Error message, or <c>null</c> if the call succeeded.</param>
    [JsonConstructor]
    public GetSwapTransactionResponse(string? transactionId, string? transactionData, long? timeoutBlockHeight, string? error)
    {
        this.TransactionId = transactionId;
        this.TransactionData = transactionData;
        this.TimeoutBlockHeight = timeoutBlockHeight;
        this.Error = error;
    }

    /// <summary>
    /// Creates a new failed instance of the object.
    /// </summary>
    /// <param name="error">Error message.</param>
    public GetSwapTransactionResponse(string? error)
    {
        this.Error = error;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}={5},{6}=`{7}`]",
            nameof(this.TransactionId), this.TransactionId,
            nameof(this.TransactionData), this.TransactionData.ToBoundedString(),
            nameof(this.TimeoutBlockHeight), this.TimeoutBlockHeight,
            nameof(this.Error), this.Error
        );
    }
}