using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response to <see cref="RestApiController.CreateSwapAsync"/> call.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by the REST API controller.")]
internal class ReverseCreatedResponse
{
    /// <summary>Unique identifier of the created swap. Used for subsequent API calls (e.g. claim, status polling, WebSocket subscription).</summary>
    [JsonPropertyName("id")]
    public string Id { get; }

    /// <summary>Current status of the swap (usually <c>swap.created</c> right after creation).</summary>
    [JsonPropertyName("status")]
    public string Status { get; }

    /// <summary>Lightning invoice (BOLT11) that the user must pay to trigger the on-chain lockup.</summary>
    [JsonPropertyName("invoice")]
    public string Invoice { get; }

    /// <summary>
    /// On-chain address (derived from the provided claim public key) where the funds will be sent once the invoice is paid. This is the address the user will eventually claim
    /// from.
    /// </summary>
    [JsonPropertyName("claimAddress")]
    public string ClaimAddress { get; }

    /// <summary>Public key (in hex) that Boltz will use for the refund path if the swap times out. Useful for constructing refund transactions if needed.</summary>
    [JsonPropertyName("refundPublicKey")]
    public string RefundPublicKeyHex { get; }

    /// <summary>Serialized Taproot swap tree. Contains the full script details (claim and refund branches) needed to build the claim or refund transaction.</summary>
    [JsonPropertyName("swapTree")]
    public string SwapTree { get; }

    /// <summary>Block height on the target chain after which the swap expires and a refund becomes possible.</summary>
    [JsonPropertyName("timeoutBlockHeight")]
    public long TimeoutBlockHeight { get; }

    /// <summary>Amount in satoshis that will be locked up on-chain once the Lightning invoice is paid. This may differ slightly from the invoice amount due to fees.
    /// </summary>
    [JsonPropertyName("onchainAmount")]
    public long OnchainAmountSats { get; }

    /// <summary>Blinding key used for confidential transactions on Liquid. <c>null</c> for Bitcoin only operation.</summary>
    [JsonPropertyName("blindingKey")]
    public string? BlindingKey { get; }

    /// <summary>Address to which funds will be refunded in case of timeout. <c>null</c> for Bitcoin only operation.</summary>
    [JsonPropertyName("refundAddress")]
    public string? RefundAddress { get; }

    /// <summary>Hash of the selected swap pair. We set pair hash to the public key of the selected Electrum swap provider.</summary>
    [JsonPropertyName("pairHash")]
    public string? PairHash { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="id">Unique identifier of the created swap. Used for subsequent API calls (e.g. claim, status polling, WebSocket subscription).</param>
    /// <param name="status">Current status of the swap (usually <c>swap.created</c> right after creation).</param>
    /// <param name="invoice">Lightning invoice (BOLT11) that the user must pay to trigger the on-chain lockup.</param>
    /// <param name="claimAddress">On-chain address (derived from the provided claim public key) where the funds will be sent once the invoice is paid. This is the address the user
    /// will eventually claim from.</param>
    /// <param name="refundPublicKeyHex">Public key (in hex) that Boltz will use for the refund path if the swap times out. Useful for constructing refund transactions if needed.
    /// </param>
    /// <param name="swapTree">Serialized Taproot swap tree. Contains the full script details (claim and refund branches) needed to build the claim or refund transaction.</param>
    /// <param name="timeoutBlockHeight">Block height on the target chain after which the swap expires and a refund becomes possible.</param>
    /// <param name="onchainAmountSats">Amount in satoshis that will be locked up on-chain once the Lightning invoice is paid. This may differ slightly from the invoice amount due
    /// to fees.</param>
    /// <param name="blindingKey">Blinding key used for confidential transactions on Liquid. <c>null</c> for Bitcoin only operation.</param>
    /// <param name="refundAddress">Address to which funds will be refunded in case of timeout. <c>null</c> for Bitcoin only operation.</param>
    /// <param name="pairHash">Hash of the selected swap pair. We set pair hash to the public key of the selected Electrum swap provider.</param>
    public ReverseCreatedResponse(string id, string status, string invoice, string claimAddress, string refundPublicKeyHex, string swapTree, long timeoutBlockHeight,
        long onchainAmountSats, string? blindingKey, string? refundAddress, string? pairHash)
    {
        this.Id = id;
        this.Status = status;
        this.Invoice = invoice;
        this.ClaimAddress = claimAddress;
        this.RefundPublicKeyHex = refundPublicKeyHex;
        this.SwapTree = swapTree;
        this.TimeoutBlockHeight = timeoutBlockHeight;
        this.OnchainAmountSats = onchainAmountSats;
        this.BlindingKey = blindingKey;
        this.RefundAddress = refundAddress;
        this.PairHash = pairHash;
    }
}