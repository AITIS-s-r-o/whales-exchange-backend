using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Single input within a deserialized Electrum transaction.
/// </summary>
/// <remarks>Note that the structure may be incomplete as we do not need all fields.</remarks>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumTransactionInput
{
    /// <summary><c>true</c> if the input is a coinbase input (i.e., it spends a coinbase transaction), <c>false</c> otherwise.</summary>
    [JsonPropertyName("coinbase")]
    public bool Coinbase { get; }

    /// <summary>Sequence number for this input.</summary>
    [JsonPropertyName("nsequence")]
    public ulong Sequence { get; }

    /// <summary>Transaction hash of the previous output being spent in hex format.</summary>
    [JsonPropertyName("prevout_hash")]
    public string PrevoutHash { get; }

    /// <summary>Index of the previous output being spent.</summary>
    [JsonPropertyName("prevout_n")]
    public int PrevoutIndex { get; }

    /// <summary>Unlocking script for this input in hex format.</summary>
    [JsonPropertyName("scriptSig")]
    public string ScriptSig { get; }

    /// <summary>List of hex-encoded witness stack items for this input.</summary>
    [JsonPropertyName("witness")]
    public string[] Witness { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="coinbase"><c>true</c> if the input is a coinbase input (i.e., it spends a coinbase transaction), <c>false</c> otherwise.</param>
    /// <param name="sequence">Sequence number for this input.</param>
    /// <param name="prevoutHash">Transaction hash of the previous output being spent in hex format.</param>
    /// <param name="prevoutIndex">Index of the previous output being spent.</param>
    /// <param name="scriptSig">Unlocking script for this input in hex format.</param>
    /// <param name="witness">List of hex-encoded witness stack items for this input.</param>
    [JsonConstructor]
    public ElectrumTransactionInput(bool coinbase, ulong sequence, string prevoutHash, int prevoutIndex, string scriptSig, string[] witness)
    {
        this.Coinbase = coinbase;
        this.Sequence = sequence;
        this.PrevoutHash = prevoutHash;
        this.PrevoutIndex = prevoutIndex;
        this.ScriptSig = scriptSig;
        this.Witness = witness;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}={3},{4}=`{5}`,{6}={7},{8}=`{9}`,|{10}|={11}]",
            nameof(this.Coinbase), this.Coinbase,
            nameof(this.Sequence), this.Sequence,
            nameof(this.PrevoutHash), this.PrevoutHash,
            nameof(this.PrevoutIndex), this.PrevoutIndex,
            nameof(this.ScriptSig), this.ScriptSig,
            nameof(this.Witness), this.Witness.Length
        );
    }
}