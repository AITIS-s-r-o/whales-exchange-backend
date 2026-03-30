using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Description of a swap provider coming from Electrum RPC.
/// </summary>
internal class ElectrumSwapProvider
{
    /// <summary>Swap provider's fee in percent.</summary>
    [JsonPropertyName("percentage_fee")]
    public decimal PercentageFee { get; }

    /// <summary>Maximum amount for a forward swap in satoshis.</summary>
    [JsonPropertyName("max_forward_sat")]
    public long MaxAmountForwardSat { get; }

    /// <summary>Maximum amount for a reverse swap in satoshis.</summary>
    [JsonPropertyName("max_reverse_sat")]
    public long MaxAmountReverseSat { get; }

    /// <summary>Minimum amount for a swap in satoshis.</summary>
    [JsonPropertyName("min_amount_sat")]
    public long MinAmountSat { get; }

    /// <summary>Mining fees required for a swap in satoshis.</summary>
    [JsonPropertyName("prepayment")]
    public long SwapPrepayment { get; }

    /// <summary>Current mining fees for a single transaction in satoshis.</summary>
    [JsonPropertyName("mining_fee")]
    public long MiningFeeSat { get; }

    /// <summary>UNIX timestamp of the latest offer from this provider in seconds.</summary>
    [JsonPropertyName("timestamp")]
    public long TimestampSec { get; }

    /// <summary>Provider's public key in hexadecimal string format.</summary>
    [JsonPropertyName("server_pubkey")]
    public string Pubkey { get; }

    /// <summary>Number of proof of work bits in the provider's profile.</summary>
    [JsonPropertyName("pow_bits")]
    public int PoWBits { get; }

    /// <summary>Provider's public key in <c>npub*</c> format.</summary>
    [JsonPropertyName("server_npub")]
    public string Npub { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="percentageFee">Swap provider's fee in percent.</param>
    /// <param name="maxAmountForwardSat">Maximum amount for a forward swap in satoshis.</param>
    /// <param name="maxAmountReverseSat">Maximum amount for a reverse swap in satoshis.</param>
    /// <param name="minAmountSat">Minimum amount for a swap in satoshis.</param>
    /// <param name="swapPrepayment">Mining fees required for a swap in satoshis.</param>
    /// <param name="miningFeeSat">Current mining fees for a single transaction in satoshis.</param>
    /// <param name="timestampSec">UNIX timestamp of the latest offer from this provider in seconds.</param>
    /// <param name="pubkey">Provider's public key in hexadecimal string format.</param>
    /// <param name="poWBits">Number of proof of work bits in the provider's profile.</param>
    /// <param name="npub">Provider's public key in <c>npub*</c> format.</param>
    public ElectrumSwapProvider(decimal percentageFee, long maxAmountForwardSat, long maxAmountReverseSat, long minAmountSat, long swapPrepayment, long miningFeeSat,
        long timestampSec, string pubkey, int poWBits, string npub)
    {
        this.PercentageFee = percentageFee;
        this.MaxAmountForwardSat = maxAmountForwardSat;
        this.MaxAmountReverseSat = maxAmountReverseSat;
        this.MinAmountSat = minAmountSat;
        this.SwapPrepayment = swapPrepayment;
        this.MiningFeeSat = miningFeeSat;
        this.TimestampSec = timestampSec;
        this.Pubkey = pubkey;
        this.PoWBits = poWBits;
        this.Npub = npub;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}={3},{4}={5},{6}={7},{8}={9},{10}={11},{12}={13},{14}=`{15}`,{16}={17},{18}=`{19}`]",
            nameof(this.PercentageFee), this.PercentageFee,
            nameof(this.MaxAmountForwardSat), this.MaxAmountForwardSat,
            nameof(this.MaxAmountReverseSat), this.MaxAmountReverseSat,
            nameof(this.MinAmountSat), this.MinAmountSat,
            nameof(this.SwapPrepayment), this.SwapPrepayment,
            nameof(this.MiningFeeSat), this.MiningFeeSat,
            nameof(this.TimestampSec), this.TimestampSec,
            nameof(this.Pubkey), this.Pubkey,
            nameof(this.PoWBits), this.PoWBits,
            nameof(this.Npub), this.Npub
        );
    }
}