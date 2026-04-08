using System;
using WhalesExchangeBackend.SharedLib.Helpers;
using WhalesSecret.TradeScriptLib.Logging;
using Xunit;

namespace WhalesExchangeBackend.SharedLibUnitTests.Helpers;

/// <summary>
/// Tests for <see cref="ProviderPresenceCalculator"/>.
/// </summary>
public class ProviderPresenceCalculatorTests
{
    /// <summary>Timestamp used in tests.</summary>
    private static readonly DateTime testTime = new(year: 2025, month: 4, day: 3, hour: 12, minute: 0, second: 0, DateTimeKind.Utc);

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Theory data for <see cref="CalculatePresentSlots(DateTime, DateTime, DateTime, int)"/> test.</summary>
    public static TheoryData<DateTime, DateTime, DateTime, int> CalculatePresentSlotsData =>
       new()
       {
            { testTime.AddMinutes(2), testTime.AddMinutes(3), testTime, 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(18), testTime, 1 },
            { testTime.AddMinutes(2), testTime.AddMinutes(28), testTime, 1 },
            { testTime.AddMinutes(2), testTime.AddMinutes(38), testTime, 1 },
            { testTime.AddMinutes(2), testTime.AddMinutes(98), testTime, 1 },

            // Server started after the provider was last seen - should not count missed slots.
            { testTime.AddMinutes(2), testTime.AddMinutes(3), testTime.AddMinutes(17), 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(18), testTime.AddMinutes(17), 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(28), testTime.AddMinutes(17), 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(38), testTime.AddMinutes(17), 1 },
            { testTime.AddMinutes(2), testTime.AddMinutes(98), testTime.AddMinutes(17), 1 },
       };

    /// <summary>Theory data for <see cref="CalculateMissedSlots(DateTime, DateTime, DateTime, int)"/> test.</summary>
    public static TheoryData<DateTime, DateTime, DateTime, int> CalculateMissedSlotsData =>
       new()
       {
            { testTime.AddMinutes(2), testTime.AddMinutes(3), testTime, 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(18), testTime, 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(28), testTime, 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(38), testTime, 1 },
            { testTime.AddMinutes(2), testTime.AddMinutes(98), testTime, 5 },

            // Server started after the provider was last seen - should not count missed slots.
            { testTime.AddMinutes(2), testTime.AddMinutes(3), testTime.AddMinutes(17), 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(18), testTime.AddMinutes(17), 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(28), testTime.AddMinutes(17), 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(38), testTime.AddMinutes(17), 0 },
            { testTime.AddMinutes(2), testTime.AddMinutes(98), testTime.AddMinutes(17), 4 },
       };

    /// <summary>
    /// Tests <see cref="ProviderPresenceCalculator.CalculatePresentSlots"/> method.
    /// </summary>
    /// <param name="prevLastSeen">UTC time when the provider was last seen according to the backend database.</param>
    /// <param name="newLastSeen">UTC time when the provider was last seen according to the latest data from Electrum.</param>
    /// <param name="serverStartTime">UTC timestamp when the current backend instance started.</param>
    /// <param name="expectedResult">Expected number of presence slots.</param>
    [Theory]
    [MemberData(nameof(CalculatePresentSlotsData))]
    public void CalculatePresentSlots(DateTime prevLastSeen, DateTime newLastSeen, DateTime serverStartTime, int expectedResult)
    {
        this.log.Debug("*");

        int actualResult = ProviderPresenceCalculator.CalculatePresentSlots(prevLastSeen: prevLastSeen, newLastSeen: newLastSeen, serverStartTime: serverStartTime);
        Assert.Equal(expectedResult, actualResult);

        this.log.Debug("$");
    }

    /// <summary>
    /// Tests <see cref="ProviderPresenceCalculator.CalculateMissedSlots(DateTime, DateTime, DateTime)"/> method.
    /// </summary>
    /// <param name="prevLastSeen">UTC time when the provider was last seen according to the backend database.</param>
    /// <param name="newLastSeen">UTC time when the provider was last seen according to the latest data from Electrum.</param>
    /// <param name="serverStartTime">UTC timestamp when the current backend instance started.</param>
    /// <param name="expectedResult">Expected number of missed presence slots.</param>
    [Theory]
    [MemberData(nameof(CalculateMissedSlotsData))]
    public void CalculateMissedSlots(DateTime prevLastSeen, DateTime newLastSeen, DateTime serverStartTime, int expectedResult)
    {
        this.log.Debug("*");

        int actualResult = ProviderPresenceCalculator.CalculateMissedSlots(prevLastSeen: prevLastSeen, newLastSeen: newLastSeen, serverStartTime: serverStartTime);
        Assert.Equal(expectedResult, actualResult);

        this.log.Debug("$");
    }
}