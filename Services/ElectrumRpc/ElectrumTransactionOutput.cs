using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Single output within a deserialized Electrum transaction.
/// </summary>
/// <remarks>Note that the structure may be incomplete as we do not need all fields.</remarks>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumTransactionOutput
{
    /// <summary>Destination address of this output.</summary>
    [JsonPropertyName("address")]
    public string Address { get; }

    /// <summary>Value of the output in satoshis.</summary>
    [JsonPropertyName("value_sats")]
    public long ValueSats { get; }

    /// <summary>Locking script for this output in hex format.</summary>
    [JsonPropertyName("scriptPubKey")]
    public string ScriptPubKey { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="address">Destination address of this output.</param>
    /// <param name="valueSats">Value of the output in satoshis.</param>
    /// <param name="scriptPubKey">Locking script for this output in hex format.</param>
    [JsonConstructor]
    public ElectrumTransactionOutput(string address, long valueSats, string scriptPubKey)
    {
        this.Address = address;
        this.ValueSats = valueSats;
        this.ScriptPubKey = scriptPubKey;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}={3},{4}=`{5}`]",
            nameof(this.Address), this.Address,
            nameof(this.ValueSats), this.ValueSats,
            nameof(this.ScriptPubKey), this.ScriptPubKey
        );
    }
}