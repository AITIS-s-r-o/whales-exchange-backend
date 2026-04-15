using System;
using System.Threading;

namespace WhalesExchangeBackend.SharedLib.Utils.Sync;

/// <summary>
/// Disposable interface that allows releasing the underlying <see cref="IReleasable"/> object.
/// </summary>
internal struct DisposableReleaser : IDisposable
{
    /// <summary>Underlying releasable object with which this releaser is bound.</summary>
    private readonly IReleasable releasableObject;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>Creates a new releaser bound to an underlying releasable object.</summary>
    /// <param name="releasableObject">Acquired releasable object.</param>
    public DisposableReleaser(IReleasable releasableObject)
    {
        this.disposedValueLock = new();
        this.releasableObject = releasableObject;
    }

    /// <summary>
    /// Frees resources used by the object. Both kinds of resources managed and unmanaged are freed based on whether the method is called from <see cref="Dispose()"/> or not.
    /// </summary>
    /// <param name="disposing"><c>true</c> if the method is called from <see cref="Dispose()"/>, <c>false</c> otherwise.</param>
    private void Dispose(bool disposing)
    {
        lock (this.disposedValueLock)
        {
            if (this.disposedValue)
                return;

            this.disposedValue = true;
        }

        if (disposing)
        {
            this.releasableObject.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}