using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Electrum transaction with its data in hex format.
/// </summary>
internal class ElectrumTransactionWithData
{
    /// <summary>Deserialized transaction.</summary>
    public ElectrumTransaction Transaction { get; }

    /// <summary>Raw transaction data in hex format.</summary>
    public string RawDataHex { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="transaction">Deserialized transaction.</param>
    /// <param name="rawHexData">Raw transaction data in hex format.</param>
    public ElectrumTransactionWithData(ElectrumTransaction transaction, string rawHexData)
    {
        this.Transaction = transaction;
        this.RawDataHex = rawHexData;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`]",
            nameof(this.Transaction), this.Transaction,
            nameof(this.RawDataHex), this.RawDataHex.ToBoundedString(64)
        );
    }
}