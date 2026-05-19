using System.Globalization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Information about funding transaction of a forward swap.
/// </summary>
public class FundingInfo
{
    /// <summary>ID of the funding transaction.</summary>
    public string TransactionId { get; }

    /// <summary>Index of the funding output.</summary>
    public int OutputIndex { get; }

    /// <summary>Raw funding transaction data in hex format.</summary>
    public string TransactionData { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="transactionId">ID of the funding transaction.</param>
    /// <param name="outputIndex">Index of the funding output.</param>
    /// <param name="transactionData">Raw funding transaction data in hex format.</param>
    public FundingInfo(string transactionId, int outputIndex, string transactionData)
    {
        this.TransactionId = transactionId;
        this.OutputIndex = outputIndex;
        this.TransactionData = transactionData;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}={3},{4}=`{5}`]",
            nameof(this.TransactionId), this.TransactionId,
            nameof(this.OutputIndex), this.OutputIndex,
            nameof(this.TransactionData), this.TransactionData.ToBoundedString()
        );
    }
}