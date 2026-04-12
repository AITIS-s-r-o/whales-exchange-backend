using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using WhalesExchangeBackend.SharedLib.Data;
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

    /// <summary>Collection that stores swaps, or <c>null</c> if it is not initialized yet.</summary>
    public DbSet<DbSwap>? SwapsSet { get; set; }

    /// <summary>Collection that stores swaps.</summary>
    public DbSet<DbSwap> Swaps => this.SwapsSet!;

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
        this.CreateModelDbSwap(modelBuilder);

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

    /// <summary>
    /// Creates model for the swaps table.
    /// </summary>
    /// <param name="modelBuilder">Database model builder.</param>
    private void CreateModelDbSwap(ModelBuilder modelBuilder)
    {
        this.log.Debug("*");

        EntityTypeBuilder<DbSwap> entity = modelBuilder.Entity<DbSwap>();

        _ = entity
            .HasKey(q => q.Id);

        _ = entity
            .Property(q => q.FrontendId)
            .IsRequired()
            .HasMaxLength(16);

        _ = entity
            .Property(q => q.ProviderPubkey)
            .IsRequired()
            .HasMaxLength(64);

        _ = entity
            .Property(q => q.IsForward)
            .IsRequired();

        _ = entity
            .Property(q => q.Status)
            .IsRequired();

        _ = entity
            .Property(q => q.AmountToPaySats)
            .IsRequired();

        _ = entity
            .Property(q => q.AmountToReceiveSats)
            .IsRequired();

        _ = entity
            .Property(q => q.LockupAddress)
            .IsUnicode()
            .IsRequired(false)
            .HasMaxLength(64);

        _ = entity
            .Property(q => q.LockupOutputIndex)
            .IsRequired(false);

        _ = entity
            .Property(q => q.FundingTxId)
            .IsRequired(false)
            .HasMaxLength(64);

        _ = entity
            .Property(q => q.CreatedTime)
            .IsRequired();

        _ = entity
            .Property(q => q.AcceptedTime)
            .IsRequired(false);

        _ = entity
            .Property(q => q.FundingTime)
            .IsRequired(false);

        _ = entity
            .Property(q => q.SpentTime)
            .IsRequired(false);

        _ = entity
            .Property(q => q.FailTime)
            .IsRequired(false);

        _ = entity
            .Property(q => q.FundingTxData)
            .IsRequired(false)
            .HasMaxLength(2 * 100 * 1024);

        _ = entity
            .HasOne(q => q.Provider)
            .WithMany()
            .HasForeignKey(q => q.ProviderPubkey)
            .OnDelete(DeleteBehavior.Cascade);

        _ = entity
            .HasIndex(q => q.ProviderPubkey);

        _ = entity
            .HasIndex(q => q.IsForward);

        _ = entity
            .HasIndex(q => q.FrontendId);

        _ = entity
            .HasIndex(q => q.Status);

        this.log.Debug("$");
    }
}