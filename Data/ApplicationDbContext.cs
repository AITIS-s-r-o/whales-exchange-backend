using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Data;

/// <summary>
/// Database context that represents a session with the database.
/// </summary>
internal class ApplicationDbContext : DbContext
{
    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Collection that stores swap providers and parameters of their latest offer, or <c>null</c> if it is not initialized yet.</summary>
    public DbSet<DbSwapProvider>? SwapProviderSet { get; set; }

    /// <summary>Collection that stores swap providers and parameters of their latest offer.</summary>
    public DbSet<DbSwapProvider> SwapProviders => this.SwapProviderSet!;

    /// <inheritdoc cref="DbContext"/>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) :
        base(options)
    {
    }

    /// <inheritdoc cref="RelationalDatabaseFacadeExtensions.BeginTransaction(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade, IsolationLevel)" />
    public IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.Serializable)
    {
        return this.Database.BeginTransaction(isolationLevel);
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        this.log.Debug("*");

        this.CreateModelDbSwapProvider(modelBuilder);

        base.OnModelCreating(modelBuilder);

        this.log.Debug("$");
    }

    /// <summary>
    /// Creates model for the swap providers table.
    /// </summary>
    /// <param name="modelBuilder">Database model builder.</param>
    private void CreateModelDbSwapProvider(ModelBuilder modelBuilder)
    {
        this.log.Debug("*");

        EntityTypeBuilder<DbSwapProvider> entity = modelBuilder.Entity<DbSwapProvider>();

        _ = entity
            .HasKey(q => q.Pubkey);

        _ = entity
            .Property(q => q.Pubkey)
            .IsUnicode()
            .IsRequired()
            .HasMaxLength(64);

        _ = entity
            .Property(q => q.FirstSeen)
            .IsRequired();

        _ = entity
            .Property(q => q.LastSeen)
            .IsRequired();

        _ = entity
            .Property(q => q.PoWBits)
            .IsRequired();

        _ = entity
            .Property(q => q.PercentageFeeForward)
            .IsRequired()
            .HasPrecision(precision: 5, scale: 2);

        _ = entity
            .Property(q => q.PercentageFeeReverse)
            .IsRequired()
            .HasPrecision(precision: 5, scale: 2);

        _ = entity
            .Property(q => q.MinAmountForwardSat)
            .IsRequired();

        _ = entity
            .Property(q => q.MinAmountReverseSat)
            .IsRequired();

        _ = entity
            .Property(q => q.MaxAmountForwardSat)
            .IsRequired();

        _ = entity
            .Property(q => q.MaxAmountReverseSat)
            .IsRequired();

        _ = entity
            .Property(q => q.MiningFeeForwardSat)
            .IsRequired();

        _ = entity
            .Property(q => q.MiningFeeReverseSat)
            .IsRequired();

        _ = entity
            .Property(q => q.SlotsPresent)
            .IsRequired();

        _ = entity
            .Property(q => q.SlotsMissed)
            .IsRequired();

        _ = entity
            .HasIndex(q => q.Pubkey);

        _ = entity
            .HasIndex(q => q.LastSeen);

        this.log.Debug("$");
    }
}