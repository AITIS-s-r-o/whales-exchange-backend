using WhalesExchangeBackend.Utils.Sync;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Data.Repository;

/// <summary>
/// Base class for database repositories.
/// </summary>
internal abstract class RepositoryBase
{
    /// <summary>Instance logger.</summary>
    protected WsLogger log { get; }

    /// <summary>Factory for the database context.</summary>
    protected ApplicationDbContextFactory dbContextFactory { get; }

    /// <summary>Database repository lock.</summary>
    protected AsyncLock dbLock { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="dbContextFactory">Factory for the database context.</param>
    /// <param name="dbLocks">Collection of database repository locks.</param>
    /// <param name="repositoryName">Name of the repository.</param>
    protected RepositoryBase(ApplicationDbContextFactory dbContextFactory, DbLocks dbLocks, string repositoryName)
    {
        this.log = new(this.GetType().FullName!);
        this.log.Debug($"* {nameof(repositoryName)}='{repositoryName}'");

        this.dbContextFactory = dbContextFactory;
        this.dbLock = dbLocks.GetLock(repositoryName);

        this.log.Debug("$");
    }
}