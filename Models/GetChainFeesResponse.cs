using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response to <see cref="RestApiController.GetChainFeesAsync"/> call.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class GetChainFeesResponse
{
    /// <summary>Bitcoin fee rate in satoshis per kilo-virtual-byte, or <c>null</c> if not available</summary>
    [JsonPropertyName("BTC")]
    public long? BtcFeeRate { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="btcFeeRate">Bitcoin fee rate in satoshis per kilo-virtual-byte, or <c>null</c> if not available.</param>
    [JsonConstructor]
    public GetChainFeesResponse(long? btcFeeRate)
    {
        this.BtcFeeRate = btcFeeRate;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1}]",
            nameof(this.BtcFeeRate), this.BtcFeeRate
        );
    }
}