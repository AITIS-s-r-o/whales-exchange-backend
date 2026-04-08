using System;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Helpers;

/// <summary>
/// Helper class for calculating the presence of providers in the system.
/// </summary>
/// <remarks>
/// In Electrum Swap protocol, the providers refresh their offers roughly every <c>10</c> minutes. We define 'presence slots' every <see cref="PresenceSlotMinutes"/> minutes.
/// If we see a new offer from a provider in a slot, we count it as 'present' for that slot. If the provider is seen at some time later, we can calculate how many slots it missed.
/// <para>In this class, "forward swap" means BTC->LN - i.e. the user sends on-chain, receives off-chain.</para>
/// </remarks>
internal static class ProviderPresenceCalculator
{
    /// <summary>Length of the presence slot in minutes.</summary>
    private const int PresenceSlotMinutes = 15;

    /// <summary>Class logger.</summary>
    private static readonly WsLogger clog = WsLogger.GetCurrentClassLogger();

    /// <summary>
    /// Calculates the number of slots that should be added to the provider's present slots.
    /// </summary>
    /// <param name="prevLastSeen">UTC time when the provider was last seen according to the database.</param>
    /// <param name="newLastSeen">UTC time when the provider was last seen according to the latest data from Electrum.</param>
    /// <param name="serverStartTime">UTC timestamp when the current backend instance started.</param>
    /// <returns><c>0</c> if a new slot has not started yet, <c>1</c> if the presence should be accounted for.</returns>
    public static int CalculatePresentSlots(DateTime prevLastSeen, DateTime newLastSeen, DateTime serverStartTime)
    {
        clog.Debug($"* {nameof(prevLastSeen)}={prevLastSeen},{nameof(newLastSeen)}={newLastSeen},{nameof(serverStartTime)}={serverStartTime}");

        CalculateSlotTimes(prevLastSeen: prevLastSeen, newLastSeen: newLastSeen, prevLastSeenSlotEnd: out DateTime prevLastSeenSlotEnd,
            newLastSeenSlotStart: out DateTime newLastSeenSlotStart);

        if (prevLastSeenSlotEnd < serverStartTime)
        {
            // Ignore slots when the server was not running.
            prevLastSeenSlotEnd = serverStartTime;
        }

        int result = 0;
        if (prevLastSeenSlotEnd <= newLastSeenSlotStart)
            result = 1;

        clog.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Calculates the number of slots that a provider missed since it was last seen.
    /// </summary>
    /// <param name="prevLastSeen">UTC time when the provider was last seen according to the database.</param>
    /// <param name="newLastSeen">UTC time when the provider was last seen according to the latest data from Electrum.</param>
    /// <param name="serverStartTime">UTC timestamp when the current backend instance started.</param>
    /// <returns>Number of slots that provider missed.</returns>
    public static int CalculateMissedSlots(DateTime prevLastSeen, DateTime newLastSeen, DateTime serverStartTime)
    {
        clog.Debug($"* {nameof(prevLastSeen)}={prevLastSeen},{nameof(newLastSeen)}={newLastSeen},{nameof(serverStartTime)}={serverStartTime}");

        CalculateSlotTimes(prevLastSeen: prevLastSeen, newLastSeen: newLastSeen, prevLastSeenSlotEnd: out DateTime prevLastSeenSlotEnd,
            newLastSeenSlotStart: out DateTime newLastSeenSlotStart);

        if (prevLastSeenSlotEnd < serverStartTime)
        {
            // Ignore slots when the server was not running.
            prevLastSeenSlotEnd = serverStartTime;
        }

        int result = 0;
        if (prevLastSeenSlotEnd < newLastSeenSlotStart)
        {
            TimeSpan diff = newLastSeenSlotStart - prevLastSeenSlotEnd;
            result = (int)(diff.TotalMinutes / PresenceSlotMinutes);
        }

        clog.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Based on previous and new last-seen timestamps, calculates the end of the slot when the provider was previously seen and the start of the slot when the provider was newly
    /// seen.
    /// </summary>
    /// <param name="prevLastSeen">UTC time when the provider was last seen according to the database.</param>
    /// <param name="newLastSeen">UTC time when the provider was last seen according to the latest data from Electrum.</param>
    /// <param name="prevLastSeenSlotEnd">This is filled with the end of the slot during which the provider was previously last seen.</param>
    /// <param name="newLastSeenSlotStart">This is filled with the start of the slot during which the provider was last seen.</param>
    private static void CalculateSlotTimes(DateTime prevLastSeen, DateTime newLastSeen, out DateTime prevLastSeenSlotEnd, out DateTime newLastSeenSlotStart)
    {
        int minute = prevLastSeen.Minute - (prevLastSeen.Minute % PresenceSlotMinutes) + PresenceSlotMinutes;
        prevLastSeenSlotEnd = new(year: prevLastSeen.Year, month: prevLastSeen.Month, day: prevLastSeen.Day, hour: prevLastSeen.Hour, minute: minute, second: 0, DateTimeKind.Utc);

        minute = newLastSeen.Minute - (newLastSeen.Minute % PresenceSlotMinutes);
        newLastSeenSlotStart = new(year: newLastSeen.Year, month: newLastSeen.Month, day: newLastSeen.Day, hour: newLastSeen.Hour, minute: minute, second: 0, DateTimeKind.Utc);
    }
}