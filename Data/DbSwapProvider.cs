using System;
using System.Globalization;
using WhalesExchangeBackend.SharedLib.Helpers;

namespace WhalesExchangeBackend.Data;

/// <summary>
/// Description of a license in the database.
/// </summary>
internal class DbSwapProvider
{
    /// <summary>Public key of the swap provider as a hex string.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public string Pubkey { get; set; }

    /// <summary>UTC time when the provider was first seen.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public DateTime FirstSeen { get; set; }

    /// <summary>UTC time when the provider was last seen.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public DateTime LastSeen { get; set; }

    /// <summary>Amount of PoW the provider used for its profile.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public int PoWBits { get; set; }

    /// <summary>Forward swap provider fee in percent.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public decimal PercentageFeeForward { get; set; }

    /// <summary>Reverse swap provider fee in percent.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public decimal PercentageFeeReverse { get; set; }

    /// <summary>Minimum amount for a forward swap in satoshis.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long MinAmountForwardSat { get; set; }

    /// <summary>Minimum amount for a reverse swap in satoshis.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long MinAmountReverseSat { get; set; }

    /// <summary>Maximum amount for a forward swap in satoshis.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long MaxAmountForwardSat { get; set; }

    /// <summary>Maximum amount for a reverse swap in satoshis.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long MaxAmountReverseSat { get; set; }

    /// <summary>Mining fee for forward swaps in satoshis.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long MiningFeeForwardSat { get; set; }

    /// <summary>Mining fee for reverse swaps in satoshis.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long MiningFeeReverseSat { get; set; }

    /// <summary>Total number of slots that the provider was present.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    /// <seealso cref="ProviderPresenceCalculator"/>
    public int SlotsPresent { get; set; }

    /// <summary>Total number of slots that the provider missed.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    /// <seealso cref="ProviderPresenceCalculator"/>
    public int SlotsMissed { get; set; }

    /// <summary>
    /// Creates an empty instance of the object.
    /// </summary>
    public DbSwapProvider()
    {
        this.Pubkey = string.Empty;
    }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="pubkey">Public key of the swap provider as a hex string.</param>
    /// <param name="firstSeen">UTC time when the provider was first seen.</param>
    /// <param name="lastSeen">UTC time when the provider was last seen.</param>
    /// <param name="poWBits">Amount of PoW the provider used for its profile.</param>
    /// <param name="percentageFeeForward">Forward swap provider fee in percent.</param>
    /// <param name="percentageFeeReverse">Reverse swap provider fee in percent.</param>
    /// <param name="minAmountForwardSat">Minimum amount for a forward swap in satoshis.</param>
    /// <param name="minAmountReverseSat">Minimum amount for a reverse swap in satoshis.</param>
    /// <param name="maxAmountForwardSat">Maximum amount for a forward swap in satoshis.</param>
    /// <param name="maxAmountReverseSat">Maximum amount for a reverse swap in satoshis.</param>
    /// <param name="miningFeeForwardSat">Mining fee for forward swaps in satoshis.</param>
    /// <param name="miningFeeReverseSat">Mining fee for reverse swaps in satoshis.</param>
    /// <param name="slotsPresent">Total number of slots that the provider was present.</param>
    /// <param name="slotsMissed">Total number of slots that the provider missed.</param>
    public DbSwapProvider(string pubkey, DateTime firstSeen, DateTime lastSeen, int poWBits, decimal percentageFeeForward, decimal percentageFeeReverse, long minAmountForwardSat,
        long minAmountReverseSat, long maxAmountForwardSat, long maxAmountReverseSat, long miningFeeForwardSat, long miningFeeReverseSat, int slotsPresent, int slotsMissed)
    {
        this.Pubkey = pubkey;
        this.FirstSeen = firstSeen;
        this.LastSeen = lastSeen;
        this.PoWBits = poWBits;
        this.PercentageFeeForward = percentageFeeForward;
        this.PercentageFeeReverse = percentageFeeReverse;
        this.MinAmountForwardSat = minAmountForwardSat;
        this.MinAmountReverseSat = minAmountReverseSat;
        this.MaxAmountForwardSat = maxAmountForwardSat;
        this.MaxAmountReverseSat = maxAmountReverseSat;
        this.MiningFeeForwardSat = miningFeeForwardSat;
        this.MiningFeeReverseSat = miningFeeReverseSat;
        this.SlotsPresent = slotsPresent;
        this.SlotsMissed = slotsMissed;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}={3},{4}={5},{6}={7},{8}={9},{10}={11},{12}={13},{14}={15},{16}={17},{18}={19},{20}={21},{22}={23},{24}={25},{26}={27}]",
            nameof(this.Pubkey), this.Pubkey,
            nameof(this.FirstSeen), this.FirstSeen,
            nameof(this.LastSeen), this.LastSeen,
            nameof(this.PoWBits), this.PoWBits,
            nameof(this.PercentageFeeForward), this.PercentageFeeReverse,
            nameof(this.PercentageFeeReverse), this.PercentageFeeReverse,
            nameof(this.MinAmountForwardSat), this.MinAmountForwardSat,
            nameof(this.MinAmountReverseSat), this.MinAmountReverseSat,
            nameof(this.MaxAmountForwardSat), this.MaxAmountForwardSat,
            nameof(this.MaxAmountReverseSat), this.MaxAmountReverseSat,
            nameof(this.MiningFeeForwardSat), this.MiningFeeForwardSat,
            nameof(this.MiningFeeReverseSat), this.MiningFeeReverseSat,
            nameof(this.SlotsPresent), this.SlotsPresent,
            nameof(this.SlotsMissed), this.SlotsMissed
        );
    }
}