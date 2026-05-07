using System.Globalization;
using WhalesExchangeBackend.Controllers;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response to <see cref="RestApiController.RemoveSwapAsync(RemoveSwapRequest)"/> call.
/// </summary>
internal class RemoveSwapResponse : RestResponseBase
{
    /// <summary>
    /// Creates a new instance of the object for the successful call.
    /// </summary>
    public RemoveSwapResponse() :
        base(success: true, data: null, error: null)
    {
    }

    /// <summary>
    /// Creates a new instance of the object for the failed call.
    /// </summary>
    /// <param name="error">Error message.</param>
    public RemoveSwapResponse(string error) :
        base(success: false, data: null, error)
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`]",
            nameof(this.Success), this.Success,
            nameof(this.Error), this.Error
        );
    }
}