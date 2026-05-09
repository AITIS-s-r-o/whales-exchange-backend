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

    /// <summary>Transaction fee in satoshis.</summary>
    [JsonPropertyName("fee")]
    public long FeeSats { get; }

    /// <summary><c>true</c> if the transaction is in the mempool, <c>false</c> if it has at least one confirmation.</summary>
    [JsonIgnore]
    public bool InMempool
        => this.BlockHeight == 0;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="blockHeight">Block height of the transaction.</param>
    /// <param name="transactionHash">Transaction ID in hex format.</param>
    /// <param name="feeSats">Transaction fee in satoshis.</param>
    [JsonConstructor]
    public AddressHistoryInfo(long blockHeight, string transactionHash, long feeSats)
    {
        this.BlockHeight = blockHeight;
        this.TransactionHash = transactionHash;
        this.FeeSats = feeSats;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`,{4}={5}]",
            nameof(this.BlockHeight), this.BlockHeight,
            nameof(this.TransactionHash), this.TransactionHash,
            nameof(this.FeeSats), this.FeeSats
        );
    }
}