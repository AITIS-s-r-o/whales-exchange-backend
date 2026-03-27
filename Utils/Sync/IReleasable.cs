namespace WhalesExchangeBackend.Utils.Sync;

/// <summary>
/// Interface for releasable synchronization primitives.
/// </summary>
internal interface IReleasable
{
    /// <summary>
    /// Releases the acquired synchronization object.
    /// </summary>
    public void Release();
}