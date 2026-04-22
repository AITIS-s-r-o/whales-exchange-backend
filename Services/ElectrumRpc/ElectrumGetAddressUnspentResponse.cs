using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Response to Electrum <c>getaddressunspent</c> RPC call.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumGetAddressUnspentResponse : List<AddressUnspentInfo>
{
    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("{");

        int i = 1;
        foreach (AddressUnspentInfo item in this)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"${i}: {item}");
            i++;
        }

        _ = sb.AppendLine("}");
        return sb.ToString();
    }
}