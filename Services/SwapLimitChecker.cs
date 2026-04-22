using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Service that checks client swap limits.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class SwapLimitChecker
{
    /// <summary>Maximum number of uncommitted swaps for each IP address.</summary>
    private const int MaximumUncommittedSwapsForIp = 5;

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Mapping of client IP addresses to frontend swap IDs.</summary>
    /// <remarks>All access has to be protected by <see cref="dataLock"/>.</remarks>
    private readonly Dictionary<string, HashSet<string>> ipToSwapsMap;

    /// <summary>Mapping of frontend swap IDs to IP addresses of their clients.</summary>
    /// <remarks>All access has to be protected by <see cref="dataLock"/>.</remarks>
    private readonly Dictionary<string, string> swapToIpMap;

    /// <summary>Lock object to be used when accessing <see cref="ipToSwapsMap"/>, and <see cref="swapToIpMap"/>.</summary>
    private readonly Lock dataLock;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    public SwapLimitChecker()
    {
        this.log.Debug("*");

        this.ipToSwapsMap = new();
        this.swapToIpMap = new();
        this.dataLock = new();

        this.log.Debug("$");
    }

    /// <summary>
    /// Tried to increment the number of uncommitted swaps created from the given IP address.
    /// </summary>
    /// <param name="ipAddress">Remote IP address of the client.</param>
    /// <param name="frontendSwapId">Frontend swap ID.</param>
    /// <returns><c>true</c> if a new swap can be created for the IP address, <c>false</c> otherwise.</returns>
    public bool RegisterSwap(string ipAddress, string frontendSwapId)
    {
        this.log.Debug($"* {nameof(ipAddress)}='{ipAddress}',{nameof(frontendSwapId)}='{frontendSwapId}'");

        bool result = false;
        int uncommittedSwapCount;

        lock (this.dataLock)
        {
            if (!this.ipToSwapsMap.TryGetValue(ipAddress, out HashSet<string>? swaps))
            {
                swaps = new HashSet<string>();
                this.ipToSwapsMap[ipAddress] = swaps;
            }

            if (swaps.Count < MaximumUncommittedSwapsForIp)
            {
                _ = swaps.Add(frontendSwapId);
                result = true;
            }

            uncommittedSwapCount = swaps.Count;
            this.swapToIpMap[frontendSwapId] = ipAddress;
        }

        this.log.Debug($"Number of uncommitted swaps originated from IP '{ipAddress}' is {uncommittedSwapCount}.");

        this.log.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Tries to decrement the number of uncommitted swaps created from the given IP address.
    /// </summary>
    /// <param name="frontendSwapId">Frontend swap ID.</param>
    /// <returns><c>true</c> if the number of uncommitted swaps was decremented for the given IP address, <c>false</c> otherwise.</returns>
    public bool UnregisterSwap(string frontendSwapId)
    {
        this.log.Debug($"* {nameof(frontendSwapId)}='{frontendSwapId}'");

        bool result = false;
        long uncommittedSwapCount = 0;
        string? ipAddress;

        lock (this.dataLock)
        {
            if (this.swapToIpMap.Remove(frontendSwapId, out ipAddress))
            {
                if (this.ipToSwapsMap.TryGetValue(ipAddress, out HashSet<string>? swaps))
                {
                    _ = swaps.Remove(frontendSwapId);
                    uncommittedSwapCount = swaps.Count;

                    if (uncommittedSwapCount == 0)
                        _ = this.ipToSwapsMap.Remove(ipAddress);

                    result = true;
                }
            }
        }

        if (ipAddress is not null)
            this.log.Debug($"Number of uncommitted swaps originated from IP '{ipAddress}' is now {uncommittedSwapCount}.");

        this.log.Debug($"$={result}");
        return result;
    }
}