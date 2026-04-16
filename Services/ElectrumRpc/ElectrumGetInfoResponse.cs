using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Services.ElectrumRpc;

/// <summary>
/// Response to Electrum <c>getinfo</c> RPC call.
/// </summary>
/// <remarks>The response is incomplete as we do not utilize other members.</remarks>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class ElectrumGetInfoResponse
{
    /// <summary>Height of Electrum client blockchain.</summary>
    [JsonPropertyName("blockchain_height")]
    public int BlockchainHeight { get; }

    /// <summary>Height of Electrum server blockchain.</summary>
    [JsonPropertyName("server_height")]
    public int ServerHeight { get; }

    /// <summary><c>true</c> if the Electrum client is connected to the Electrum server, <c>false</c> otherwise.</summary>
    [JsonPropertyName("connected")]
    public bool Connected { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="blockchainHeight">Height of Electrum client blockchain.</param>
    /// <param name="serverHeight">Height of Electrum server blockchain.</param>
    /// <param name="connected"><c>true</c> if the Electrum client is connected to the Electrum server, <c>false</c> otherwise.</param>
    public ElectrumGetInfoResponse(int blockchainHeight, int serverHeight, bool connected)
    {
        this.BlockchainHeight = blockchainHeight;
        this.ServerHeight = serverHeight;
        this.Connected = connected;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}={3},{4}={5}]",
            nameof(this.BlockchainHeight), this.BlockchainHeight,
            nameof(this.ServerHeight), this.ServerHeight,
            nameof(this.Connected), this.Connected
        );
    }
}