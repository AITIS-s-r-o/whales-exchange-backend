using System.Globalization;
using System.Threading;
using WhalesExchangeBackend.Controllers;
using WhalesExchangeBackend.SharedLib.Data;

namespace WhalesExchangeBackend.Models;

/// <summary>
/// Result of <see cref="RestApiController.CreateForwardSwapAsync(CreateSwapRequest, DbSwapProvider, string, string, CancellationToken)"/>
/// and <see cref="RestApiController.CreateReverseSwapAsync(CreateSwapRequest, DbSwapProvider, string, string, CancellationToken)"/> and methods.
/// </summary>
internal readonly struct CreateSwapResult
{
    /// <summary>Description of a swap in the database, or <c>null</c> if the method failed.</summary>
    public DbSwap? Swap { get; }

    /// <summary>Response to <see cref="RestApiController.CreateSwapAsync"/> call.</summary>
    public CreateSwapResponse Response { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="swap">Description of a swap in the database, or <c>null</c> if the method failed.</param>
    /// <param name="response">Response to <see cref="RestApiController.CreateSwapAsync"/> call.</param>
    public CreateSwapResult(DbSwap? swap, CreateSwapResponse response)
    {
        this.Swap = swap;
        this.Response = response;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`]",
            nameof(this.Swap), this.Swap,
            nameof(this.Response), this.Response
        );
    }
}