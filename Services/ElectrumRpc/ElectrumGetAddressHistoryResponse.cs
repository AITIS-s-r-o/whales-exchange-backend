using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Response to Electrum <c>getaddresshistory</c> RPC call.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumGetAddressHistoryResponse : List<AddressHistoryInfo>
{
    /// <summary>Maximum number of entries to show in <see cref="ToString"/>.</summary>
    private const int MaxHistoryEntriesToLog = 5;

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("{");

        int i = 0;
        if (this.Count > MaxHistoryEntriesToLog)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"... {this.Count - MaxHistoryEntriesToLog} entries omitted ...");
            i = this.Count - MaxHistoryEntriesToLog;
        }

        for (; i < this.Count; i++)
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"${i + 1}: {this[i]}");

        _ = sb.AppendLine("}");
        return sb.ToString();
    }
}