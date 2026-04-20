using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response to <see cref="RestApiController.BroadcastTransactionAsync(BroadcastTransactionRequest)"/> call.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class BroadcastTransactionResponse
{
    /// <summary>Transaction ID in hex format.</summary>
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="transactionId">Transaction ID in hex format.</param>
    [JsonConstructor]
    public BroadcastTransactionResponse(string transactionId)
    {
        this.TransactionId = transactionId;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`]",
            nameof(this.TransactionId), this.TransactionId.ToBoundedString()
        );
    }
}