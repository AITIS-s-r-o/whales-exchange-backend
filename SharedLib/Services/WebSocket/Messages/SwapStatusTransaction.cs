using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Description of a Bitcoin transaction associated with the swap update.
/// </summary>
internal class SwapStatusTransaction
{
    /// <summary>Raw Bitcoin transaction in hex format, or <c>null</c> if not available.</summary>
    [JsonPropertyName("hex")]
    public string? Hex { get; }

    /// <summary>Bitcoin transaction ID in hex format.</summary>
    [JsonPropertyName("id")]
    public string Id { get; }

    /// <summary>Refund Bitcoin transaction ID in hex format, or <c>null</c> if no refund transaction is available.</summary>
    [JsonPropertyName("refundTxId")]
    public string? RefundTxId { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="hex">Raw Bitcoin transaction in hex format, or <c>null</c> if not available.</param>
    /// <param name="id">Bitcoin transaction ID in hex format.</param>
    /// <param name="refundTxId">Refund Bitcoin transaction ID in hex format, or <c>null</c> if no refund transaction is available.</param>
    [JsonConstructor]
    public SwapStatusTransaction(string? hex, string id, string? refundTxId = null)
    {
        this.Hex = hex;
        this.Id = id;
        this.RefundTxId = refundTxId;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}=`{5}`]",
            nameof(this.Hex), this.Hex,
            nameof(this.Id), this.Id,
            nameof(this.RefundTxId), this.RefundTxId
        );
    }
}