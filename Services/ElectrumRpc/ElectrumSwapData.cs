using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Represents the internal swap data stored by Electrum's submarine swap plugin.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by the REST API controller.")]
internal class ElectrumSwapData
{
    /// <summary><c>true</c> if this is a reverse swap (LN → BTC) from the client's point of view, <c>false</c> if it is a forward swap (BTC → LN).</summary>
    [JsonPropertyName("is_reverse")]
    public bool IsReverse { get; }

    /// <summary>Absolute locktime (block height) after which a refund becomes possible.</summary>
    [JsonPropertyName("locktime")]
    public long Locktime { get; }

    /// <summary>Amount that will be locked on-chain in satoshis.</summary>
    [JsonPropertyName("onchain_amount")]
    public long OnChainAmountSats { get; }

    /// <summary>Amount of the Lightning payment in satoshis.</summary>
    [JsonPropertyName("lightning_amount")]
    public long LightningAmountSats { get; }

    /// <summary>Full redeem script of the HTLC as hex.</summary>
    [JsonPropertyName("redeem_script")]
    public string RedeemScriptHex { get; }

    /// <summary>Hash of the prepayment invoice as hex.</summary>
    /// <seealso cref="FeeInvoice"/>
    [JsonPropertyName("prepay_hash")]
    public string PrepayHashHex { get; }

    /// <summary>On-chain lockup address (P2WSH address where funds will be sent).</summary>
    [JsonPropertyName("lockup_address")]
    public string LockupAddress { get; }

    /// <summary>Address and amount to which the funding UTXO should be claimed.</summary>
    [JsonPropertyName("claim_to_output")]
    public ClaimToOutput ClaimToOutput { get; }

    /// <summary>Main swap LN invoice.</summary>
    [JsonPropertyName("invoice")]
    public string Invoice { get; }

    /// <summary>LN invoice to cover swap provider fees.</summary>
    [JsonPropertyName("fee_invoice")]
    public string FeeInvoice { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="isReverse"><c>true</c> if this is a reverse swap (LN → BTC) from the client's point of view, <c>false</c> if it is a forward swap (BTC → LN).</param>
    /// <param name="locktime">Absolute locktime (block height) after which a refund becomes possible.</param>
    /// <param name="onChainAmountSats">Amount that will be locked on-chain in satoshis.</param>
    /// <param name="lightningAmountSats">Amount of the Lightning payment in satoshis.</param>
    /// <param name="redeemScriptHex">Full redeem script of the HTLC as hex.</param>
    /// <param name="prepayHashHex">Hash of the prepayment invoice as hex.</param>
    /// <param name="lockupAddress">On-chain lockup address (P2WSH address where funds will be sent).</param>
    /// <param name="claimToOutput">Address and amount to which the funding UTXO should be claimed.</param>
    /// <param name="invoice">Main swap LN invoice.</param>
    /// <param name="feeInvoice">LN invoice to cover swap provider fees.</param>
    public ElectrumSwapData(bool isReverse, long locktime, long onChainAmountSats, long lightningAmountSats, string redeemScriptHex, string prepayHashHex, string lockupAddress,
        ClaimToOutput claimToOutput, string invoice, string feeInvoice)
    {
        this.IsReverse = isReverse;
        this.Locktime = locktime;
        this.OnChainAmountSats = onChainAmountSats;
        this.LightningAmountSats = lightningAmountSats;
        this.RedeemScriptHex = redeemScriptHex;
        this.PrepayHashHex = prepayHashHex;
        this.LockupAddress = lockupAddress;
        this.ClaimToOutput = claimToOutput;
        this.Invoice = invoice;
        this.FeeInvoice = feeInvoice;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}={3},{4}={5},{6}={7},{8}=`{9}`,{10}=`{11}`,{12}=`{13}`,{14}=`{15}`,{16}=`{17}`,{18}=`{19}`]",
            nameof(this.IsReverse), this.IsReverse,
            nameof(this.Locktime), this.Locktime,
            nameof(this.OnChainAmountSats), this.OnChainAmountSats,
            nameof(this.LightningAmountSats), this.LightningAmountSats,
            nameof(this.RedeemScriptHex), this.RedeemScriptHex,
            nameof(this.PrepayHashHex), this.PrepayHashHex,
            nameof(this.LockupAddress), this.LockupAddress,
            nameof(this.ClaimToOutput), this.ClaimToOutput,
            nameof(this.Invoice), this.Invoice,
            nameof(this.FeeInvoice), this.FeeInvoice
        );
    }
}