using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Deserialized Bitcoin transaction returned by the Electrum wallet's <c>deserialize</c> RPC command.
/// </summary>
/// <remarks>Note that the structure may be incomplete as we do not need all fields.</remarks>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumTransaction
{
    /// <summary>Version number of the transaction.</summary>
    [JsonPropertyName("version")]
    public int Version { get; }

    /// <summary>Locktime of the transaction.</summary>
    [JsonPropertyName("lockTime")]
    public ulong LockTime { get; }

    /// <summary>List of inputs in the transaction.</summary>
    [JsonPropertyName("inputs")]
    public List<ElectrumTransactionInput> Inputs { get; }

    /// <summary>List of outputs in the transaction.</summary>
    [JsonPropertyName("outputs")]
    public List<ElectrumTransactionOutput> Outputs { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="version">Version number of the transaction.</param>
    /// <param name="lockTime">Locktime of the transaction.</param>
    /// <param name="inputs">List of inputs in the transaction.</param>
    /// <param name="outputs">List of outputs in the transaction.</param>
    [JsonConstructor]
    public ElectrumTransaction(int version, ulong lockTime, List<ElectrumTransactionInput> inputs, List<ElectrumTransactionOutput> outputs)
    {
        this.Version = version;
        this.LockTime = lockTime;
        this.Inputs = inputs;
        this.Outputs = outputs;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}={3},{4}={5},{6}={7}]",
            nameof(this.Version), this.Version,
            nameof(this.LockTime), this.LockTime,
            nameof(this.Inputs), this.Inputs.LogJoin(),
            nameof(this.Outputs), this.Outputs.LogJoin()
        );
    }
}