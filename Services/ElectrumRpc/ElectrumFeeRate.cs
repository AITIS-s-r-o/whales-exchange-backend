using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Information about current fee rates returned by the Electrum wallet's <c>getfeerate</c> RPC command.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumFeeRate
{
    /// <summary>Human readable fee rate description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; }

    /// <summary>Identifier of the policy used for fee estimation.</summary>
    [JsonPropertyName("policy")]
    public string Policy { get; }

    /// <summary>Fee rate in satoshis per kilo-virtual-byte, or <c>null</c> if the information is not available.</summary>
    [JsonPropertyName("sat/kvB")]
    public long? FeeRateKvB { get; }

    /// <summary>Additional description of the fee rate condition.</summary>
    [JsonPropertyName("Tooltip")]
    public string Tooltip { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="description">Human readable fee rate description.</param>
    /// <param name="policy">Identifier of the policy used for fee estimation.</param>
    /// <param name="feeRateKvB">Fee rate as satoshis per kilo-virtual-byte, or <c>null</c> if the information is not available.</param>
    /// <param name="tooltip">Additional description of the fee rate condition.</param>
    public ElectrumFeeRate(string description, string policy, long? feeRateKvB, string tooltip)
    {
        this.Description = description;
        this.Policy = policy;
        this.FeeRateKvB = feeRateKvB;
        this.Tooltip = tooltip;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}={5},{6}=`{7}`]",
            nameof(this.Description), this.Description,
            nameof(this.Policy), this.Policy,
            nameof(this.FeeRateKvB), this.FeeRateKvB,
            nameof(this.Tooltip), this.Tooltip
        );
    }
}