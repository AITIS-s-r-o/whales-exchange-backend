using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        int i = 0;
        foreach ((string key, ElectrumSwapProvider provider) in this)
        {
            i++;
            _ = sb.Append('#');
            _ = sb.Append(i);
            _ = sb.Append(": ");
            _ = sb.Append(key);
            _ = sb.Append("->");
            _ = sb.Append(provider);
            _ = sb.Append(';');
        }

        _ = sb.Append('}');
        return sb.ToString();
    }
}