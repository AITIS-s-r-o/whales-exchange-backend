using System.Globalization;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Description of a Bitcoin transaction associated with the swap update.
/// </summary>
internal class SwapStatusTransaction
{
    /// <summary>Raw Bitcoin transaction in hex format, or <c>null</c> if not available.</summary>
    public string? Hex { get; }

    /// <summary>Bitcoin transaction ID in hex format.</summary>
    public string Id { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="hex">Raw Bitcoin transaction in hex format, or <c>null</c> if not available.</param>
    /// <param name="id">Bitcoin transaction ID in hex format.</param>
    public SwapStatusTransaction(string? hex, string id)
    {
        this.Hex = hex;
        this.Id = id;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`]",
            nameof(this.Hex), this.Hex,
            nameof(this.Id), this.Id
        );
    }
}