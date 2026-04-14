using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Information about an unspent output for an address.
/// </summary>
internal class AddressUnspentInfo
{
    /// <summary>Height of the Bitcoin block in which the transaction is included.</summary>
    [JsonPropertyName("height")]
    public int BlockHeight { get; }

    /// <summary>Hash of the transaction.</summary>
    [JsonPropertyName("tx_hash")]
    public string TransactionHash { get; }

    /// <summary>Index of the relevant output in the transaction.</summary>
    [JsonPropertyName("tx_pos")]
    public int OutputIndex { get; }

    /// <summary>Value of the output in satoshis.</summary>
    [JsonPropertyName("value")]
    public long AmountSats { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="blockHeight">Height of the Bitcoin block in which the transaction is included.</param>
    /// <param name="transactionHash">Hash of the transaction.</param>
    /// <param name="outputIndex">Index of the relevant output in the transaction.</param>
    /// <param name="amountSats">Value of the output in satoshis.</param>
    public AddressUnspentInfo(int blockHeight, string transactionHash, int outputIndex, long amountSats)
    {
        this.BlockHeight = blockHeight;
        this.TransactionHash = transactionHash;
        this.OutputIndex = outputIndex;
        this.AmountSats = amountSats;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`,{4}={5},{6}={7}]",
            nameof(this.BlockHeight), this.BlockHeight,
            nameof(this.TransactionHash), this.TransactionHash,
            nameof(this.OutputIndex), this.OutputIndex,
            nameof(this.AmountSats), this.AmountSats
        );
    }
}