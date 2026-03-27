using System;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.Data;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Description of a swap provider in REST API.
/// </summary>
internal class RestSwapProvider
{
    /// <summary>Public key of the swap provider as a hex string.</summary>
    [JsonPropertyName("pk")]
    public string Pubkey { get; }

    /// <summary>UTC time when the provider was last seen.</summary>
    [JsonPropertyName("time")]
    public DateTime LastSeen { get; }

    /// <summary>Amount of PoW the provider used for its profile.</summary>
    [JsonPropertyName("pow")]
    public int PoWBits { get; }

    /// <summary>Forward swap provider fee in percent.</summary>
    [JsonPropertyName("fwdFee")]
    public decimal PercentageFeeForward { get; }

    /// <summary>Reverse swap provider fee in percent.</summary>
    [JsonPropertyName("revFee")]
    public decimal PercentageFeeReverse { get; }

    /// <summary>Minimum amount for a forward swap in satoshis.</summary>
    [JsonPropertyName("fwdMin")]
    public long MinAmountForwardSat { get; }

    /// <summary>Minimum amount for a reverse swap in satoshis.</summary>
    [JsonPropertyName("revMin")]
    public long MinAmountReverseSat { get; }

    /// <summary>Maximum amount for a forward swap in satoshis.</summary>
    [JsonPropertyName("fwdMax")]
    public long MaxAmountForwardSat { get; }

    /// <summary>Maximum amount for a reverse swap in satoshis.</summary>
    [JsonPropertyName("revMax")]
    public long MaxAmountReverseSat { get; }

    /// <summary>Mining fee for forward swaps in satoshis.</summary>
    [JsonPropertyName("fwdMining")]
    public long MiningFeeForwardSat { get; }

    /// <summary>Mining fee for reverse swaps in satoshis.</summary>
    [JsonPropertyName("revMining")]
    public long MiningFeeReverseSat { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="pubkey">Public key of the swap provider as a hex string.</param>
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
    [JsonConstructor]
    public RestSwapProvider(string pubkey, DateTime lastSeen, int poWBits, decimal percentageFeeForward, decimal percentageFeeReverse, long minAmountForwardSat,
        long minAmountReverseSat, long maxAmountForwardSat, long maxAmountReverseSat, long miningFeeForwardSat, long miningFeeReverseSat)
    {
        this.Pubkey = pubkey;
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
    }

    /// <summary>
    /// Converts <see cref="DbSwapProvider"/> to <see cref="RestSwapProvider"/>.
    /// </summary>
    /// <param name="dbRecord">Swap provider database record.</param>
    /// <returns>REST swap provider description.</returns>
    public static RestSwapProvider FromDbSwapProvider(DbSwapProvider dbRecord)
    {
        return new
        (
            dbRecord.Pubkey,
            dbRecord.LastSeen,
            poWBits: dbRecord.PoWBits,
            percentageFeeForward: dbRecord.PercentageFeeForward,
            percentageFeeReverse: dbRecord.PercentageFeeReverse,
            minAmountForwardSat: dbRecord.MinAmountForwardSat,
            minAmountReverseSat: dbRecord.MinAmountReverseSat,
            maxAmountForwardSat: dbRecord.MaxAmountForwardSat,
            maxAmountReverseSat: dbRecord.MaxAmountReverseSat,
            miningFeeForwardSat: dbRecord.MiningFeeForwardSat,
            miningFeeReverseSat: dbRecord.MiningFeeReverseSat
        );
    }
}