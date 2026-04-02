using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Target output (address and amount) for claiming the funding UTXO.
/// </summary>
internal class ClaimToOutput
{
    /// <summary>Address to send the claimed funds to.</summary>
    [JsonPropertyName("address")]
    public string Address { get; }

    /// <summary>Amount to claim in satoshis.</summary>
    [JsonPropertyName("amount")]
    public long AmountSats { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimToOutput"/> class.
    /// </summary>
    /// <param name="address">Address to send claimed funds to.</param>
    /// <param name="amountSats">Amount to claim satoshis.</param>
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