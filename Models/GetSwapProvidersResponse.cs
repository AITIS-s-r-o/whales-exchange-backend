using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response to <see cref="RestApiController.GetSwapProvidersAsync"/> call.
/// </summary>
internal class GetSwapProvidersResponse : RestResponseBase
{
    /// <summary>Ordered list of swap providers. Providers are ordered first by PoW (descending) and then by public key (ascending).</summary>
    [JsonIgnore]
    public RestSwapProvider[] Providers => (RestSwapProvider[])this.Data!;

    /// <summary>
    /// Creates a new instance of the object for the successful call.
    /// </summary>
    /// <param name="providers">Ordered list of swap providers. Providers are ordered first by PoW (descending) and then by public key (ascending).</param>
    public GetSwapProvidersResponse(RestSwapProvider[] providers) :
        base(success: true, data: providers, error: null)
    {
    }

    /// <summary>
    /// Creates a new instance of the object for the failed call.
    /// </summary>
    /// <param name="error">Error message.</param>
    public GetSwapProvidersResponse(string error) :
        base(success: false, data: null, error)
    {
    }
}