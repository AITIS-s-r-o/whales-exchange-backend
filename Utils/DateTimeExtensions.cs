using System;

namespace WhalesExchangeBackend.Utils;

/// <summary>
/// Extensions related to representing time instants.
/// </summary>
internal static class DateTimeExtensions
{
    /// <summary>UNIX epoch instant expressed using ticks-derived value in seconds.</summary>
    private const long UnixEpochSeconds = 62_135_596_800;

    /// <summary>UNIX epoch instant expressed using ticks-derived value in milliseconds.</summary>
    private const long UnixEpochMilliseconds = 62_135_596_800_000;

    /// <summary>
    /// Converts <see cref="DateTime"/> instance to UNIX timestamp in seconds.
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/> instance to convert.</param>
    /// <returns>Number of seconds that have elapsed since 1970-01-01T00:00:00Z.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="dateTime"/> does not represent Coordinated Universal Time (UTC).</exception>
    public static long ToUnixTimeSeconds(this DateTime dateTime)
    {
        return (dateTime.Ticks / TimeSpan.TicksPerSecond) - UnixEpochSeconds;
    }

    /// <summary>
    /// Converts <see cref="DateTime"/> instance to UNIX timestamp in milliseconds.
    /// </summary>
    /// <param name="timestamp"><see cref="DateTime"/> instance to convert.</param>
    /// <returns>Number of milliseconds that have elapsed since 1970-01-01T00:00:00Z.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="timestamp"/> does not represent Coordinated Universal Time (UTC).</exception>
    public static long ToUnixTimeMilliseconds(this DateTime timestamp)
    {
        return (timestamp.Ticks / TimeSpan.TicksPerMillisecond) - UnixEpochMilliseconds;
    }

    /// <summary>
    /// Converts UNIX timestamp in seconds to <see cref="DateTime"/>.
    /// </summary>
    /// <param name="timestamp">UNIX timestamp in seconds.</param>
    /// <returns>Date time that corresponds to the given UNIX timestamp.</returns>
    public static DateTime FromUnixTimeSeconds(this long timestamp)
    {
        return DateTime.UnixEpoch.AddSeconds(timestamp);
    }

    /// <inheritdoc cref="FromUnixTimeSeconds(long)"/>
    public static DateTime FromUnixTimeSeconds(this ulong timestamp)
    {
        return DateTime.UnixEpoch.AddSeconds(timestamp);
    }

    /// <summary>
    /// Converts UNIX timestamp in milliseconds to <see cref="DateTime"/>.
    /// </summary>
    /// <param name="timestamp">UNIX timestamp in milliseconds.</param>
    /// <returns>Date time that corresponds to the given UNIX timestamp.</returns>
    public static DateTime FromUnixTimeMilliseconds(this long timestamp)
    {
        return DateTime.UnixEpoch.AddMilliseconds(timestamp);
    }

    /// <inheritdoc cref="FromUnixTimeMilliseconds(long)"/>
    public static DateTime FromUnixTimeMilliseconds(this ulong timestamp)
    {
        return DateTime.UnixEpoch.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// Converts UNIX timestamp in nanoseconds to <see cref="DateTime"/>.
    /// </summary>
    /// <param name="timestamp">UNIX timestamp in nanoseconds.</param>
    /// <returns>Date time that corresponds to the given UNIX timestamp.</returns>
    /// <remarks>The implementation loses precision. .NET can only express hundreds of nanoseconds, not less.</remarks>
    /// <seealso href="https://github.com/dotnet/runtime/issues/23799">Add Microseconds and Nanoseconds to TimeStamp, DateTime, DateTimeOffset, and TimeOnly</seealso>
    /// <seealso href="https://github.com/dotnet/runtime/pull/67666">[API Implementation]: Add Microseconds and Nanoseconds to TimeStamp, DateTime, DateTimeOffset, and TimeOnly
    /// </seealso>
    public static DateTime FromUnixTimeNanoseconds(this long timestamp)
    {
        long microseconds = timestamp / 1000;
        long nanosecondsRemainder = timestamp % 1000;

        return DateTime.UnixEpoch.AddMicroseconds(microseconds).AddMicroseconds(nanosecondsRemainder / 1000.0);
    }

    /// <inheritdoc cref="FromUnixTimeNanoseconds(long)"/>
    public static DateTime FromUnixTimeNanoseconds(this ulong timestamp)
    {
        ulong microseconds = timestamp / 1000;
        ulong nanosecondsRemainder = timestamp % 1000;

        return DateTime.UnixEpoch.AddMicroseconds(microseconds).AddMicroseconds(nanosecondsRemainder / 1000.0);
    }
}