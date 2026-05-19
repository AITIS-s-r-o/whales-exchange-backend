using System.Globalization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Information about refund transaction of a forward swap.
/// </summary>
public class RefundInfo
{
    /// <summary>ID of the refund transaction.</summary>
    public string TransactionId { get; }

    /// <summary>Raw refund transaction data in hex format.</summary>
    public string TransactionData { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="transactionId">ID of the refund transaction.</param>
    /// <param name="transactionData">Raw refund transaction data in hex format.</param>
    public RefundInfo(string transactionId, string transactionData)
    {
        this.TransactionId = transactionId;
        this.TransactionData = transactionData;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`]",
            nameof(this.TransactionId), this.TransactionId,
            nameof(this.TransactionData), this.TransactionData.ToBoundedString()
        );
    }
}