using System.Globalization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services.DataProvider;

/// <summary>
/// Description of the type of action that occurred on a <see cref="MonitoredAddress"/> together with the source data required for processing the action.
/// </summary>
internal class MonitoredAddressActionInfo
{
    /// <summary>Type of action.</summary>
    public MonitoredAddressAction Action { get; }

    /// <summary>Hash of the transaction.</summary>
    public string TransactionHash { get; }

    /// <summary>Index of the relevant output in the transaction.</summary>
    public int OutputIndex { get; }

    /// <summary>Raw transaction data in hex format.</summary>
    public string TransactionData { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="action">Type of action.</param>
    /// <param name="transactionHash">Hash of the transaction.</param>
    /// <param name="outputIndex">Index of the relevant output in the transaction.</param>
    /// <param name="transactionData">Raw transaction data in hex format.</param>
    public MonitoredAddressActionInfo(MonitoredAddressAction action, string transactionHash, int outputIndex, string transactionData)
    {
        this.Action = action;
        this.TransactionHash = transactionHash;
        this.OutputIndex = outputIndex;
        this.TransactionData = transactionData;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`,{4}={5},{6}=`{7}`]",
            nameof(this.Action), this.Action,
            nameof(this.TransactionHash), this.TransactionHash,
            nameof(this.OutputIndex), this.OutputIndex,
            nameof(this.TransactionData), this.TransactionData.ToBoundedString(256)
        );
    }
}