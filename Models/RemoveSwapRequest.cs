using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Request to remove an existing swap.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by JSON deserializer.")]
internal class RemoveSwapRequest
{
    /// <summary>ID of the swap to be removed.</summary>
    [JsonPropertyName("id")]
    public string Id { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="id">ID of the swap to be removed.</param>
    [JsonConstructor]
    public RemoveSwapRequest(string id)
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