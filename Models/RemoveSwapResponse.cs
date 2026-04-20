using System.Text.Json.Serialization;
using WhalesExchangeBackend.Controllers;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Response to <see cref="RestApiController.RemoveSwapAsync(string)"/> call.
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
}