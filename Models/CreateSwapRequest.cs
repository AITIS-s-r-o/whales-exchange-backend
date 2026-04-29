using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Request to create a new swap.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class CreateSwapRequest
{
    /// <summary>String identifier of the forward swap type.</summary>
    public const string ForwardSwapTypeStr = "submarine";

    /// <summary>String identifier of the order side for forward swaps.</summary>
    public const string ForwardSwapOrderSideStr = "sell";

    /// <summary>String identifier of the reverse swap type.</summary>
    public const string ReverseSwapTypeStr = "reversesubmarine";

    /// <summary>String identifier of the order side for reverse swaps.</summary>
    public const string ReverseSwapOrderSideStr = "buy";

    /// <summary>Pair ID that is supported.</summary>
    public const string SupportedPairIdStr = "BTC/BTC";

    /// <summary>Either <see cref="ForwardSwapTypeStr"/> or <see cref="ReverseSwapTypeStr"/>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; }

    /// <summary>ID of the assets being swapped. This should be set to <see cref="SupportedPairIdStr"/>.</summary>
    [JsonPropertyName("pairId")]
    public string PairId { get; }

    /// <summary>
    /// <see cref="ForwardSwapOrderSideStr"/> if <see cref="Type"/> is <see cref="ForwardSwapTypeStr"/>, <see cref="ReverseSwapOrderSideStr"/> if it is
    /// <see cref="ReverseSwapTypeStr"/>.
    /// </summary>
    [JsonPropertyName("orderSide")]
    public string OrderSide { get; }

    /// <summary>Amount the user has to pay in satoshis for reverse swaps, or <c>null</c> for forward swaps.</summary>
    [JsonPropertyName("invoiceAmount")]
    public long? InvoiceAmount { get; }

    /// <summary>Lightning invoice for forward swaps, or <c>null</c> for reverse swaps.</summary>
    [JsonPropertyName("invoice")]
    public string? Invoice { get; }

    /// <summary>Amount the user expects to receive in satoshis.</summary>
    [JsonPropertyName("expectedAmount")]
    public long ExpectedAmount { get; }

    /// <summary>Hash of the preimage.</summary>
    [JsonPropertyName("preimageHash")]
    public string PreimageHash { get; }

    /// <summary>Public key that will be used to claim the on-chain funds, or <c>null</c> for forward swaps.</summary>
    [JsonPropertyName("claimPublicKey")]
    public string? ClaimPublicKey { get; }

    /// <summary>Public key that will be used for the refund of the on-chain funds if a forward swap fails, or <c>null</c> for reverse swaps.</summary>
    [JsonPropertyName("refundPublicKey")]
    public string? RefundPublicKey { get; }

    /// <summary>Public key of the selected swap provider.</summary>
    [JsonPropertyName("pairHash")]
    public string PairHash { get; }

    /// <summary>Claim address for reverse swaps, refund address for forward swaps.</summary>
    [JsonPropertyName("clientAddress")]
    public string ClientAddress { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="type">Either <see cref="ForwardSwapTypeStr"/> or <see cref="ReverseSwapTypeStr"/>.</param>
    /// <param name="pairId">ID of the assets being swapped. This should be set to <c>BTC/BTC</c>.</param>
    /// <param name="orderSide"><see cref="ForwardSwapOrderSideStr"/> if <see cref="Type"/> is <see cref="ForwardSwapTypeStr"/>, <see cref="ReverseSwapOrderSideStr"/> if it is
    /// <see cref="ReverseSwapTypeStr"/>.</param>
    /// <param name="invoiceAmount">Amount the user has to pay in satoshis for reverse swaps, or <c>null</c> for forward swaps.</param>
    /// <param name="invoice">Lightning invoice for forward swaps, or <c>null</c> for reverse swaps.</param>
    /// <param name="expectedAmount">Amount the user expects to receive in satoshis.</param>
    /// <param name="preimageHash">Hash of the preimage.</param>
    /// <param name="claimPublicKey">Public key that will be used to claim the on-chain funds, or <c>null</c> for forward swaps.</param>
    /// <param name="refundPublicKey">Public key that will be used for the refund of the on-chain funds if a forward swap fails, or <c>null</c> for reverse swaps.</param>
    /// <param name="pairHash">Public key of the selected swap provider.</param>
    /// <param name="clientAddress">Claim address for reverse swaps, refund address for forward swaps.</param>
    [JsonConstructor]
    public CreateSwapRequest(string type, string pairId, string orderSide, long? invoiceAmount, string? invoice, long expectedAmount, string preimageHash, string? claimPublicKey,
        string? refundPublicKey, string pairHash, string clientAddress)
    {
        this.Type = type;
        this.PairId = pairId;
        this.OrderSide = orderSide;
        this.InvoiceAmount = invoiceAmount;
        this.Invoice = invoice;
        this.ExpectedAmount = expectedAmount;
        this.PreimageHash = preimageHash;
        this.ClaimPublicKey = claimPublicKey;
        this.RefundPublicKey = refundPublicKey;
        this.PairHash = pairHash;
        this.ClientAddress = clientAddress;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}=`{5}`,{6}={7},{8}=`{9}`,{10}={11},{12}=`{13}`,{14}=`{15}`,{16}=`{17}`,{18}=`{19}`,{20}=`{21}`]",
            nameof(this.Type), this.Type,
            nameof(this.PairId), this.PairId,
            nameof(this.OrderSide), this.OrderSide,
            nameof(this.InvoiceAmount), this.InvoiceAmount,
            nameof(this.Invoice), this.Invoice,
            nameof(this.ExpectedAmount), this.ExpectedAmount,
            nameof(this.PreimageHash), this.PreimageHash,
            nameof(this.ClaimPublicKey), this.ClaimPublicKey,
            nameof(this.RefundPublicKey), this.RefundPublicKey,
            nameof(this.PairHash), this.PairHash,
            nameof(this.ClientAddress), this.ClientAddress
        );
    }
}