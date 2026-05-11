using System.Globalization;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response to <see cref="RestApiController.CreateSwapAsync"/> call.
/// </summary>
internal class CreateSwapResponse : RestResponseBase
{
    /// <summary>Response returned when creating or fetching a swap.</summary>
    [JsonIgnore]
    public SwapResponse SwapResponse => (SwapResponse)this.Data!;

    /// <summary>
    /// Creates a new instance of the object for the successful call.
    /// </summary>
    /// <param name="swapResponse">Response returned when creating or fetching a swap.</param>
    public CreateSwapResponse(SwapResponse swapResponse) :
        base(success: true, data: swapResponse, error: null)
    {
    }

    /// <summary>
    /// Creates a new instance of the object for the failed call.
    /// </summary>
    /// <param name="error">Error message.</param>
    public CreateSwapResponse(string error) :
        base(success: false, data: null, error)
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`,{4}=`{5}`]",
            nameof(this.Success), this.Success,
            nameof(this.SwapResponse), this.SwapResponse,
            nameof(this.Error), this.Error
        );
    }
}