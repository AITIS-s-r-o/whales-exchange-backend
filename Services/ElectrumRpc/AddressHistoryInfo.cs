using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Information about a history entry of a Bitcoin transaction.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class AddressHistoryInfo
{
    /// <summary>Block height of the transaction.</summary>
    [JsonPropertyName("height")]
    public long BlockHeight { get; }

    /// <summary>Transaction ID in hex format.</summary>
    [JsonPropertyName("tx_hash")]
    public string TransactionHash { get; }

    /// <summary><c>true</c> if the transaction is in the mempool, <c>false</c> if it has at least one confirmation.</summary>
    [JsonIgnore]
    public bool InMempool
        => this.BlockHeight == 0;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="height">Block height of the transaction.</param>
    /// <param name="transactionId">Transaction ID in hex format.</param>
    [JsonConstructor]
    public AddressHistoryInfo(long height, string transactionId)
    {
        this.BlockHeight = height;
        this.TransactionHash = transactionId;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`]",
            nameof(this.BlockHeight), this.BlockHeight,
            nameof(this.TransactionHash), this.TransactionHash
        );
    }
}