using System;
using System.Globalization;
using WhalesExchangeBackend.SharedLib.Models;

namespace WhalesExchangeBackend.SharedLib.Data;

/// <summary>
/// Description of a swap in the database.
/// </summary>
/// <remarks>In this class, "forward swap" means BTC->LN - i.e. the user sends on-chain, receives off-chain.</remarks>
internal class DbSwap
{
    /// <summary>Length of <see cref="FrontendId"/>.</summary>
    public const int FrontendIdLength = 10;

    /// <summary>Unique ID of the swap.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long Id { get; set; }

    /// <summary>Unique ID of the swap provided to the frontend.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public string FrontendId { get; set; }

    /// <summary>Public key of the swap provider as a hex string.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public string ProviderPubkey { get; set; }

    /// <summary><c>true</c> for forward swaps, <c>false</c> for reverse swaps.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public bool IsForward { get; set; }

    /// <summary>Status of the swap.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public SwapStatus Status { get; set; }

    /// <summary>Amount the client paid or should pay (including all fees) in satoshis.</summary>
    public long AmountToPaySats { get; set; }

    /// <summary>Amount the client received or should receive in satoshis.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long AmountToReceiveSats { get; set; }

    /// <summary>Bitcoin address to which the reverse swap funding Bitcoin transaction spends the funds to be claimed by the client, or <c>null</c> for forward swaps.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public string? LockupAddress { get; set; }

    /// <summary>Index of the output in the funding Bitcoin transaction that spends to the <see cref="LockupAddress"/>, or <c>null</c> for forward swaps.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public int? LockupOutputIndex { get; set; }

    /// <summary>ID of the funding Bitcoin transaction, or <c>null</c> if not funded yet.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public string? FundingTxId { get; set; }

    /// <summary>Block height after which the swap is considered expired, or <c>null</c> if not set yet.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public long? TimeoutBlockHeight { get; set; }

    /// <summary>UTC time when the swap was created by the user.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public DateTime CreatedTime { get; set; }

    /// <summary>UTC time when the swap was created by the user, or <c>null</c> if not accepted yet.</summary>
    /// <remarks>
    /// This time value is set by this backend.
    /// <para>The setter is needed for the serializer.</para>
    /// </remarks>
    public DateTime? AcceptedTime { get; set; }

    /// <summary>UTC time when the funding Bitcoin transaction has been broadcasted by the provider, or <c>null</c> if not funded yet.</summary>
    /// <remarks>
    /// This time value is set by this backend.
    /// <para>The setter is needed for the serializer.</para>
    /// </remarks>
    public DateTime? FundingTime { get; set; }

    /// <summary>UTC time when the funding Bitcoin transaction output was spent by the client, or <c>null</c> if not spent yet.</summary>
    /// <remarks>
    /// This time value is set by this backend.
    /// <para>The setter is needed for the serializer.</para>
    /// </remarks>
    public DateTime? SpentTime { get; set; }

    /// <summary>UTC time since when the swap is considered as failed, or <c>null</c> if the swap is not failed.</summary>
    /// <remarks>
    /// This time value is set by this backend.
    /// <para>The setter is needed for the serializer.</para>
    /// </remarks>
    public DateTime? FailTime { get; set; }

    /// <summary>Funding transaction data in hex format, or <c>null</c> if not funded yet.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public string? FundingTxData { get; set; }

    /// <summary>Provider of the swap.</summary>
    /// <remarks>The setter is needed for the serializer.</remarks>
    public DbSwapProvider Provider { get; set; }

    /// <summary>
    /// Creates an empty instance of the object.
    /// </summary>
    public DbSwap()
    {
        this.FrontendId = null!;
        this.ProviderPubkey = string.Empty;
        this.Provider = null!;
    }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="id">Unique ID of the swap.</param>
    /// <param name="frontendId">Unique ID of the swap provided to the frontend.</param>
    /// <param name="providerPubkey">Public key of the swap provider as a hex string.</param>
    /// <param name="isForward"><c>true</c> for forward swaps, <c>false</c> for reverse swaps.</param>
    /// <param name="status">Status of the swap.</param>
    /// <param name="amountToPaySats">Amount the client paid or should pay (including all fees) in satoshis.</param>
    /// <param name="amountToReceiveSats">Amount the client received or should receive in satoshis.</param>
    /// <param name="lockupAddress">Bitcoin address to which the reverse swap funding Bitcoin transaction spends the funds to be claimed by the client, or <c>null</c> for forward
    /// swaps.</param>
    /// <param name="lockupOutputIndex">Index of the output in the funding Bitcoin transaction that spends to the <see cref="LockupAddress"/>, or <c>null</c> for forward swaps.
    /// </param>
    /// <param name="fundingTxId">ID of the funding Bitcoin transaction, or <c>null</c> if not funded yet.</param>
    /// <param name="timeoutBlockHeight">Block height after which the swap is considered expired, or <c>null</c> if not set yet.</param>
    /// <param name="createdTime">UTC time when the swap was created by the user.</param>
    /// <param name="acceptedTime">UTC time when the swap was created by the user, or <c>null</c> if not accepted yet.</param>
    /// <param name="fundingTime">UTC time when the funding Bitcoin transaction has been broadcasted by the provider, or <c>null</c> if not funded yet.</param>
    /// <param name="spentTime">UTC time when the funding Bitcoin transaction output was spent by the client, or <c>null</c> if not spent yet.</param>
    /// <param name="failTime">UTC time since when the swap is considered as failed, or <c>null</c> if the swap is not failed.</param>
    /// <param name="fundingTxData">Funding transaction data in hex format, or <c>null</c> if not funded yet.</param>
    /// <param name="provider">Provider of the swap.</param>
    public DbSwap(long id, string frontendId, string providerPubkey, bool isForward, SwapStatus status, long amountToPaySats, long amountToReceiveSats, string? lockupAddress,
        int? lockupOutputIndex, string? fundingTxId, long? timeoutBlockHeight, DateTime createdTime, DateTime? acceptedTime, DateTime? fundingTime, DateTime? spentTime,
        DateTime? failTime, string? fundingTxData, DbSwapProvider provider)
    {
        this.Id = id;
        this.FrontendId = frontendId;
        this.ProviderPubkey = providerPubkey;
        this.IsForward = isForward;
        this.Status = status;
        this.AmountToPaySats = amountToPaySats;
        this.AmountToReceiveSats = amountToReceiveSats;
        this.LockupAddress = lockupAddress;
        this.LockupOutputIndex = lockupOutputIndex;
        this.FundingTxId = fundingTxId;
        this.TimeoutBlockHeight = timeoutBlockHeight;
        this.CreatedTime = createdTime;
        this.AcceptedTime = acceptedTime;
        this.FundingTime = fundingTime;
        this.SpentTime = spentTime;
        this.FailTime = failTime;
        this.FundingTxData = fundingTxData;
        this.Provider = provider;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        string format = "[{0}={1},{2}=`{3}`,{4}=`{5}`,{6}={7},{8}={9},{10}={11},{12}={13},{14}=`{15}`,{16}={17},{18}=`{19}`,{20}={21},{22}={23},{24}={25},{26}={27},{28}={29},"
            + "{30}={31},{32}=`{33}`]";

        return string.Format
        (
            CultureInfo.InvariantCulture,
            format,
            nameof(this.Id), this.Id,
            nameof(this.FrontendId), this.FrontendId,
            nameof(this.ProviderPubkey), this.ProviderPubkey,
            nameof(this.IsForward), this.IsForward,
            nameof(this.Status), this.Status,
            nameof(this.AmountToPaySats), this.AmountToPaySats,
            nameof(this.AmountToReceiveSats), this.AmountToReceiveSats,
            nameof(this.LockupAddress), this.LockupAddress,
            nameof(this.LockupOutputIndex), this.LockupOutputIndex,
            nameof(this.FundingTxId), this.FundingTxId,
            nameof(this.TimeoutBlockHeight), this.TimeoutBlockHeight,
            nameof(this.CreatedTime), this.CreatedTime,
            nameof(this.AcceptedTime), this.AcceptedTime,
            nameof(this.FundingTime), this.FundingTime,
            nameof(this.SpentTime), this.SpentTime,
            nameof(this.FailTime), this.FailTime,
            nameof(this.FundingTxData), this.FundingTxData
        );
    }
}