using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Utils.Sync;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Data;

/// <summary>
/// Collection of database repository locks.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class DbLocks : IAsyncDisposable
{
    /// <summary>List of existing repositories.</summary>
    private static readonly string[] repositoryNames = new string[]
    {
        nameof(SwapProviderRepository),
    };

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Database repository locks mapped to the name of the repositories.</summary>
    private readonly Dictionary<string, AsyncLock> locks;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    public DbLocks()
    {
        this.log.Debug("*");

        this.disposedValueLock = new();
        this.locks = new Dictionary<string, AsyncLock>();

        foreach (string name in repositoryNames)
        {
            this.locks[name] = new AsyncLock(name);
            this.log.Debug($"Lock for repository '{name}' has been created.");
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Gets a database lock for the given repository.
    /// </summary>
    /// <param name="repositoryName">Name of the repository.</param>
    /// <returns>Database lock for the given repository.</returns>
    internal AsyncLock GetLock(string repositoryName)
    {
        if (!this.locks.TryGetValue(repositoryName, out AsyncLock? dbLock))
            throw new SanityCheckException($"Lock for repository '{repositoryName}' does not exist.");

        return dbLock;
    }

    /// <summary>
    /// Frees managed resources used by the object.
    /// </summary>
    /// <returns>A <see cref="ValueTask">task</see> that represents the asynchronous dispose operation.</returns>
    protected virtual async ValueTask DisposeCoreAsync()
    {
        this.log.Debug("*");

        lock (this.disposedValueLock)
        {
            if (this.disposedValue)
            {
                this.log.Debug("$<ALREADY_DISPOSED>");
                return;
            }

            this.disposedValue = true;
        }

        foreach ((string name, AsyncLock dbLock) in this.locks)
        {
            this.log.Debug($"Disposing lock for '{name}' repository.");
            await dbLock.DisposeAsync().ConfigureAwait(false);
        }

        this.locks.Clear();

        this.log.Debug("$");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}