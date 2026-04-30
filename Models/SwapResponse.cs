using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response returned when creating or fetching a swap.
/// </summary>
public class SwapResponse
{
    /// <summary>Unique identifier for the swap.</summary>
    [JsonPropertyName("id")]
    public string Id { get; }

    /// <summary><c>true</c> if this is a reverse swap (LN → BTC), <c>false</c> if submarine (BTC -> LN).</summary>
    [JsonPropertyName("reverse")]
    public bool Reverse { get; }

    /// <summary>Asset being used.</summary>
    [JsonPropertyName("asset")]
    public string Asset { get; }

    /// <summary>Lightning invoice for the swap, or <c>null</c> if not set.</summary>
    [JsonPropertyName("invoice")]
    public string? Invoice { get; }

    /// <summary>Lightning invoice for the provider's fees, or <c>null</c> if not set.</summary>
    [JsonPropertyName("feeInvoice")]
    public string? FeeInvoice { get; }

    /// <summary>Block height after which the swap times out and refund becomes possible.</summary>
    [JsonPropertyName("timeoutBlockHeight")]
    public long TimeoutBlockHeight { get; }

    /// <summary>Amount the user is sending in satoshis.</summary>
    [JsonPropertyName("sendAmount")]
    public long SendAmountSats { get; }

    /// <summary>Amount the user is receiving in satoshis.</summary>
    [JsonPropertyName("receiveAmount")]
    public long ReceiveAmountSats { get; }

    /// <summary>Actual amount that will be locked on-chain (may differ slightly from <see cref="ReceiveAmountSats"/> due to fees), or <c>null</c> if not set.</summary>
    [JsonPropertyName("onchainAmount")]
    public long? OnChainAmountSats { get; }

    /// <summary>Expected amount in satoshis, or <c>null</c> if not set.</summary>
    [JsonPropertyName("expectedAmount")]
    public long? ExpectedAmountSats { get; }

    /// <summary>Redeem script (hex) of the HTLC, or <c>null</c> if not set.</summary>
    [JsonPropertyName("redeemScript")]
    public string? RedeemScript { get; }

    /// <summary>On-chain lockup address (P2WSH), or <c>null</c> if not set.</summary>
    [JsonPropertyName("lockupAddress")]
    public string? LockupAddress { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="id">Unique identifier for the swap.</param>
    /// <param name="reverse"><c>true</c> if this is a reverse swap (LN → BTC), <c>false</c> if submarine (BTC -> LN).</param>
    /// <param name="asset">Asset being used.</param>
    /// <param name="invoice">Lightning invoice for the swap, or <c>null</c> if not set.</param>
    /// <param name="feeInvoice">Lightning invoice for the provider's fees, or <c>null</c> if not set.</param>
    /// <param name="timeoutBlockHeight">Block height after which the swap times out and refund becomes possible.</param>
    /// <param name="sendAmountSats">Amount the user is sending in satoshis.</param>
    /// <param name="receiveAmountSats">Amount the user is receiving in satoshis.</param>
    /// <param name="onChainAmountSats">Actual amount that will be locked on-chain (may differ slightly from <see cref="ReceiveAmountSats"/> due to fees) in satoshis, or
    /// <c>null</c> if not set.</param>
    /// <param name="expectedAmountSats">Expected amount in satoshis, or <c>null</c> if not set.</param>
    /// <param name="redeemScript">Redeem script (hex) of the HTLC, or <c>null</c> if not set.</param>
    /// <param name="lockupAddress">On-chain lockup address (P2WSH), or <c>null</c> if not set.</param>
    public SwapResponse(string id, bool reverse, string asset, string? invoice, string? feeInvoice, long timeoutBlockHeight, long sendAmountSats, long receiveAmountSats,
        long? onChainAmountSats = null, long? expectedAmountSats = null, string? redeemScript = null, string? lockupAddress = null)
    {
        this.Id = id;
        this.Reverse = reverse;
        this.Asset = asset;
        this.Invoice = invoice;
        this.FeeInvoice = feeInvoice;
        this.TimeoutBlockHeight = timeoutBlockHeight;
        this.SendAmountSats = sendAmountSats;
        this.ReceiveAmountSats = receiveAmountSats;
        this.OnChainAmountSats = onChainAmountSats;
        this.ExpectedAmountSats = expectedAmountSats;
        this.RedeemScript = redeemScript;
        this.LockupAddress = lockupAddress;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}={3},{4}=`{5}`,{6}=`{7}`,{8}=`{9}`,{10}={11},{12}={13},{14}={15},{16}={17},{18}={19},{20}=`{21}`,{22}=`{23}`]",
            nameof(this.Id), this.Id,
            nameof(this.Reverse), this.Reverse,
            nameof(this.Asset), this.Asset,
            nameof(this.Invoice), this.Invoice,
            nameof(this.FeeInvoice), this.FeeInvoice,
            nameof(this.TimeoutBlockHeight), this.TimeoutBlockHeight,
            nameof(this.SendAmountSats), this.SendAmountSats,
            nameof(this.ReceiveAmountSats), this.ReceiveAmountSats,
            nameof(this.OnChainAmountSats), this.OnChainAmountSats,
            nameof(this.ExpectedAmountSats), this.ExpectedAmountSats,
            nameof(this.RedeemScript), this.RedeemScript,
            nameof(this.LockupAddress), this.LockupAddress
        );
    }
}