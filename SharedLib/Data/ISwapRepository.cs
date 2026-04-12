using System.Threading.Tasks;
using WhalesExchangeBackend.SharedLib.Exceptions;

namespace WhalesExchangeBackend.SharedLib.Data;

/// <summary>
/// Provider of access to swaps in the database.
/// </summary>
internal interface ISwapRepository
{
    /// <summary>
    /// Gets swaps identified by their frontend IDs from the databased.
    /// </summary>
    /// <param name="frontendIds">Frontend IDs of the swaps to retrieve.</param>
    /// <returns>List of the requested swaps. If any of the ID is not found in the database, the corresponding item in the list is set to <c>null</c>.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public Task<DbSwap?[]> GetSwapsByFrontentIdsAsync(string[] frontendIds);
}