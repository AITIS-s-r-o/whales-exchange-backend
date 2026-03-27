using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using WhalesExchangeBackend.Services;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Data;

/// <summary>
/// Factory for the database context.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class ApplicationDbContextFactory
{
    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Server configuration helper.</summary>
    private readonly ConfigHelper configHelper;

    /// <inheritdoc cref="DbContext"/>
    public ApplicationDbContextFactory(ConfigHelper configHelper)
    {
        this.log.Debug("*");

        this.configHelper = configHelper;

        this.log.Debug("$");
    }

    /// <summary>
    /// Creates a database context.
    /// </summary>
    /// <returns>Database context.</returns>
    public ApplicationDbContext CreateDbContext()
    {
        this.log.Debug("*");

        string connectionString = this.configHelper.ConnectionString;
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        ApplicationDbContext context = new(options);

        this.log.Debug("$");
        return context;
    }
}