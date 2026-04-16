using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Response to Electrum <c>getaddressunspent</c> RPC call.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumGetAddressUnspentResponse : List<AddressUnspentInfo>
{
}