using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Response to Electrum <c>get_submarine_swap_providers</c> RPC call.
/// </summary>
/// <remarks>Keys are swap provider public keys in <c>npub*</c> format.</remarks>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumGetSubmarineSwapProviderResponse : Dictionary<string, ElectrumSwapProvider>
{
}