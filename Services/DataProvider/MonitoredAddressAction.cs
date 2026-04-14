namespace WhalesExchangeBackend.Services.DataProvider;

/// <summary>
/// Type of actions on <see cref="MonitoredAddress"/>es.
/// </summary>
internal enum MonitoredAddressAction
{
    /// <summary>Transaction spending to the address is in the mempool.</summary>
    InMempool = 1,

    /// <summary>Transaction spending to the address is confirmed.</summary>
    Confirmed = 2,

    /// <summary>Monitoring of the address has expired.</summary>
    Timeout = 3,
}