using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Decoded Lightning invoice returned by the Electrum wallet's <c>wex_decode_invoice</c> RPC command.
/// </summary>
/// <remarks>Note that the structure may be incomplete as we do not need all fields.</remarks>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumLightningInvoice
{
    /// <summary>Amount in millisatoshis.</summary>
    [JsonPropertyName("amount_msat")]
    public long AmountMsat { get; }

    /// <summary>Payment hash of the invoice in hex format.</summary>
    [JsonPropertyName("rhash")]
    public string PaymentHashHex { get; }

    /// <summary>UNIX timestamp in seconds when the invoice was created.</summary>
    [JsonPropertyName("time")]
    public long CreateTime { get; }

    /// <summary>Number of seconds until the invoice expires after <see cref="CreateTime"/>.</summary>
    [JsonPropertyName("expiry")]
    public long ExpirySeconds { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="amountMsat">Amount in millisatoshis.</param>
    /// <param name="paymentHashHex">Payment hash of the invoice in hex format.</param>
    /// <param name="createTime">UNIX timestamp in seconds when the invoice was created.</param>
    /// <param name="expirySeconds">Number of seconds until the invoice expires after <paramref name="createTime"/>.</param>
    public ElectrumLightningInvoice(long amountMsat, string paymentHashHex, long createTime, long expirySeconds)
    {
        this.AmountMsat = amountMsat;
        this.PaymentHashHex = paymentHashHex;
        this.CreateTime = createTime;
        this.ExpirySeconds = expirySeconds;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`,{4}={5},{6}={7}]",
            nameof(this.AmountMsat), this.AmountMsat,
            nameof(this.PaymentHashHex), this.PaymentHashHex,
            nameof(this.CreateTime), this.CreateTime,
            nameof(this.ExpirySeconds), this.ExpirySeconds
        );
    }
}