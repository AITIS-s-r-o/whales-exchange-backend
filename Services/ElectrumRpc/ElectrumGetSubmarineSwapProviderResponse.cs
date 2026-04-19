using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Response to Electrum <c>get_submarine_swap_providers</c> RPC call.
/// </summary>
/// <remarks>Keys are swap provider public keys in <c>npub*</c> format.</remarks>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumGetSubmarineSwapProviderResponse : Dictionary<string, ElectrumSwapProvider>
{
    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder sb = new();
        _ = sb.Append('{');

        int i = 1;
        foreach ((string key, ElectrumSwapProvider provider) in this)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"${i}: {key} -> {provider}");
            i++;
        }

        _ = sb.Append('}');
        return sb.ToString();
    }
}