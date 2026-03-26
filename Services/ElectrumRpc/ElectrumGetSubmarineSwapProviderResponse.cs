using System.Collections.Generic;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Response to Electrum <c>get_submarine_swap_providers</c> RPC call.
/// </summary>
/// <remarks>Keys are swap provider public keys in <c>npub*</c> format.</remarks>
internal class ElectrumGetSubmarineSwapProviderResponse : Dictionary<string, ElectrumSwapProvider>
{
}