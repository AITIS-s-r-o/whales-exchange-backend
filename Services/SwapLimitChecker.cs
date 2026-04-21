using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Service that checks user swap limits.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class SwapLimitChecker
{
    /// <summary>Maximum number of uncommitted swaps for each IP address.</summary>
    private const int MaximumUncommittedSwapsForIp = 5;

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Lock object to be used when accessing <see cref="activeSwaps"/>.</summary>
    private readonly Lock dataLock;

    /// <summary>Mapping of IP addresses to their respective number of active swaps.</summary>
    private readonly Dictionary<string, long> activeSwaps;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    public SwapLimitChecker()
    {
        this.log.Debug("*");

        this.activeSwaps = new();
        this.dataLock = new();

        this.log.Debug("$");
    }

    /// <summary>
    /// Tried to increment the number of active swaps created from the given IP address.
    /// </summary>
    /// <param name="ipAddress">Remote IP address of the user.</param>
    /// <returns><c>true</c> if a new swap can be created for the IP address, <c>false</c> otherwise.</returns>
    public bool TryIncrementSwapCount(string ipAddress)
    {
        this.log.Debug($"* {nameof(ipAddress)}='{ipAddress}'");

        bool result = false;
        long activeSwapCount;

        lock (this.dataLock)
        {
            _ = this.activeSwaps.TryGetValue(ipAddress, out activeSwapCount);

            if (activeSwapCount < MaximumUncommittedSwapsForIp)
            {
                this.activeSwaps[ipAddress] = activeSwapCount + 1;
                result = true;
            }
        }

        this.log.Debug($"Number of active swaps originated from IP '{ipAddress}' is {activeSwapCount}.");

        this.log.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Tries to decrement the number of active swaps created from the given IP address.
    /// </summary>
    /// <param name="ipAddress">Remote IP address of the user.</param>
    /// <returns><c>true</c> if the number of active swaps was decremented for the given IP address, <c>false</c> otherwise.</returns>
    public bool TryDecrementSwapCount(string ipAddress)
    {
        this.log.Debug($"* {nameof(ipAddress)}='{ipAddress}'");

        bool result = false;
        long activeSwapCount;

        lock (this.dataLock)
        {
            _ = this.activeSwaps.TryGetValue(ipAddress, out activeSwapCount);

            if (activeSwapCount > 0)
            {
                this.activeSwaps[ipAddress] = activeSwapCount - 1;
                result = true;
            }
        }

        this.log.Debug($"Number of active swaps originated from IP '{ipAddress}' is now {activeSwapCount}.");

        this.log.Debug($"$={result}");
        return result;
    }
}