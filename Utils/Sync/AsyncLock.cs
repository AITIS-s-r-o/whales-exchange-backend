using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Utils.Sync;

/// <summary>
/// Synchronization object used to prevent race conditions with disposable release via <see cref="DisposableReleaser"/>.
/// </summary>
internal class AsyncLock : IAsyncDisposable, IDisposable, IReleasable
{
    /// <summary>Instance logger.</summary>
    private readonly WsLogger log;

    /// <summary>Error indicating that waiting for the lock was cancelled.</summary>
    public const string ErrorCancelled = nameof(AsyncLock) + "_" + nameof(ErrorCancelled);

    /// <summary>Lock object itself.</summary>
    private readonly SemaphoreSlim lockObject;

    /// <summary>Name of the instance for logging purposes only.</summary>
    private readonly string instanceName;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="instanceName">Name of the instance for logging purposes only.</param>
    public AsyncLock(string instanceName)
    {
        this.instanceName = instanceName;
        this.log = new(this.GetType().FullName!, this.instanceName);

        this.log.Trace("*");

        this.disposedValueLock = new();
        this.lockObject = new(initialCount: 1, maxCount: 1);

        this.log.Trace("$");
    }

    /// <summary>
    /// Acquires the lock and provides <see cref="IDisposable"/> interface for its releasing.
    /// <para>Acquired lock must be released via <see cref="Release"/>.</para>
    /// </summary>
    /// <returns><see cref="IDisposable"/> interface that should be used to release the acquired lock.</returns>
    public IDisposable Enter()
    {
        this.log.Trace("*");

        this.lockObject.Wait();
        IDisposable result = new DisposableReleaser(this);

        this.log.Trace("$");
        return result;
    }

    /// <inheritdoc cref="Enter()"/>
    public async Task<IDisposable> EnterAsync()
    {
        this.log.Trace("*");

        await this.lockObject.WaitAsync().ConfigureAwait(false);
        IDisposable result = new DisposableReleaser(this);

        this.log.Trace("$");
        return result;
    }

    /// <summary>Gets if the lock is locked or not.</summary>
    public bool IsLocked
        => this.lockObject.CurrentCount == 0;

    /// <summary>
    /// Releases lock previously acquired using <see cref="Enter()"/> or <see cref="EnterAsync()"/>.
    /// </summary>
    public void Release()
    {
        this.log.Trace("*");

        _ = this.lockObject.Release();

        this.log.Trace("$");
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.instanceName;
    }

    /// <summary>
    /// Synchronously frees managed resources used by the object.
    /// </summary>
    private void DisposeInternalSync()
    {
        this.log.Debug("*");

        this.log.Debug("Disposing lock object.");
        this.lockObject.Dispose();

        this.log.Debug("$");
    }

    /// <summary>
    /// Frees managed resources used by the object.
    /// </summary>
    /// <returns>A <see cref="ValueTask">task</see> that represents the asynchronous dispose operation.</returns>
    protected virtual ValueTask DisposeCoreAsync()
    {
        this.log.Debug("*");

        lock (this.disposedValueLock)
        {
            if (this.disposedValue)
            {
                this.log.Debug("$<ALREADY_DISPOSED>");
                return default;
            }

            this.disposedValue = true;
        }

        this.DisposeInternalSync();

        this.log.Debug("$");
        return default;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Frees resources used by the object. Both kinds of resources managed and unmanaged are freed based on whether the method is called from <see cref="Dispose()"/> or not.
    /// </summary>
    /// <param name="disposing"><c>true</c> if the method is called from <see cref="Dispose()"/>, <c>false</c> otherwise.</param>
    protected virtual void Dispose(bool disposing)
    {
        this.log.Debug($"* {nameof(disposing)}={disposing}");

        lock (this.disposedValueLock)
        {
            if (this.disposedValue)
            {
                this.log.Debug("$<ALREADY_DISPOSED>");
                return;
            }

            this.disposedValue = true;
        }

        this.DisposeInternalSync();

        this.log.Debug("$");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}