using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Request to broadcast a transaction.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class BroadcastTransactionRequest
{
    /// <summary>Currency of the transaction.</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; }

    /// <summary>Transaction data in hex format.</summary>
    [JsonPropertyName("transactionHex")]
    public string TransactionHex { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="currency">Currency of the transaction.</param>
    /// <param name="transactionHex">Transaction data in hex format.</param>
    public BroadcastTransactionRequest(string currency, string transactionHex)
    {
        this.Currency = currency;
        this.TransactionHex = transactionHex;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`]",
            nameof(this.Currency), this.Currency,
            nameof(this.TransactionHex), this.TransactionHex.ToBoundedString()
        );
    }
}