using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Target output (address and amount) for claiming the funding UTXO.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ClaimToOutput
{
    /// <summary>Address to send the claimed funds to.</summary>
    [JsonPropertyName("address")]
    public string Address { get; }

    /// <summary>Amount to claim in satoshis.</summary>
    [JsonPropertyName("amount")]
    public long AmountSats { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="address">Address to send the claimed funds to.</param>
    /// <param name="amountSats">Amount to claim in satoshis.</param>
    public ClaimToOutput(string address, long amountSats)
    {
        this.Address = address;
        this.AmountSats = amountSats;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}={3}]",
            nameof(this.Address), this.Address,
            nameof(this.AmountSats), this.AmountSats
        );
    }
}