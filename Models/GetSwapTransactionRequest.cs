using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Request to get funding transaction of a swap.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class GetSwapTransactionRequest
{
    /// <summary>Frontend swap ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="id">Frontend swap ID.</param>
    [JsonConstructor]
    public GetSwapTransactionRequest(string id)
    {
        this.Id = id;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`]",
            nameof(this.Id), this.Id
        );
    }
}