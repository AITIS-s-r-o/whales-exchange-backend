using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using WhalesExchangeBackend.Exceptions;
using WhalesExchangeBackend.Models;
using WhalesExchangeBackend.Utils;

namespace WhalesExchangeBackend.Data.Repository;

/// <summary>
/// Provider of access to swaps in the database.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class SwapRepository : RepositoryBase
{
    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="dbContextFactory">Factory for the database context.</param>
    /// <param name="dbLocks">Collection of database repository locks.</param>
    public SwapRepository(ApplicationDbContextFactory dbContextFactory, DbLocks dbLocks) :
        base(dbContextFactory, dbLocks, nameof(SwapRepository))
    {
        this.log.Debug("*$");
    }

    /// <summary>
    /// Inserts or a new reverse swap to the database.
    /// </summary>
    /// <param name="providerPubkey">Public key of the swap provider as a hex string.</param>
    /// <param name="amountToPaySats">Amount the client paid or should pay (including all fees) in satoshis.</param>
    /// <param name="amountToReceiveSats">Amount the client received or should receive in satoshis.</param>
    /// <returns>Newly created database record.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap> InsertReverseAsync(string providerPubkey, long amountToPaySats, long amountToReceiveSats)
    {
        this.log.Debug($"* {nameof(providerPubkey)}='{providerPubkey}',{nameof(amountToPaySats)}={amountToPaySats},{nameof(amountToReceiveSats)}={amountToReceiveSats}");

        DbSwap result;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwapProvider? dbSwapProvider = await db.SwapProviders.FindAsync(providerPubkey).ConfigureAwait(false);
            if (dbSwapProvider is null)
            {
                this.log.Error($"Provider with pubkey '{providerPubkey}' was not found in the database.");
                this.log.Debug("$<PROVIDER_NOT_FOUND>");
                throw new DatabaseException($"Inserting a new reverse swap to the database failed because the swap provider with pubkey '{
                    providerPubkey}' was not found in the database.");
            }

            DateTime now = DateTime.UtcNow;
            string frontendId = RandomStringGenerator.Generate(DbSwap.FrontendIdLength);
            DbSwap dbRecord = new(id: 0, frontendId: frontendId, providerPubkey: providerPubkey, isForward: false, SwapStatus.Created, amountToPaySats: amountToPaySats,
                amountToReceiveSats: amountToReceiveSats, lockupAddress: null, lockupOutputIndex: null, fundingTxId: null, timeoutBlockHeight: null, createdTime: now,
                acceptedTime: null, fundingTime: null, spentTime: null, failTime: null, dbSwapProvider);

            _ = db.Swaps.Add(dbRecord);
            _ = db.SaveChanges();
            transaction.Commit();

            result = dbRecord;
            this.log.Debug($"Reverse swap ID {result.Id} through provider '{providerPubkey}' has been added to the database.");
        }
        catch (Exception e)
        {
            this.log.Error($"Inserting a new reverse swap to the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException("Inserting a new reverse swap to the database failed.", e);
        }

        this.log.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Marks the given swap as accepted in the database and sets the lockup address for the reverse swap.
    /// </summary>
    /// <param name="id">ID of the swap.</param>
    /// <param name="lockupAddress">Lockup address for the reverse swap, or <c>null</c> for forward swap.</param>
    /// <param name="timeoutBlockHeight">Block height after which the swap is considered expired.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task MarkSwapAcceptedAsync(long id, string? lockupAddress, long timeoutBlockHeight)
    {
        this.log.Debug($"* {nameof(id)}={id},{nameof(lockupAddress)}='{lockupAddress}',{nameof(timeoutBlockHeight)}={timeoutBlockHeight}");

        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps.FindAsync(id).ConfigureAwait(false);
            if (dbRecord is null)
            {
                this.log.Error($"Swap with ID '{id}' was not found in the database.");
                this.log.Debug("$<SWAP_NOT_FOUND>");
                throw new DatabaseException($"Marking the swap with ID '{id}' as accepted in the database failed because the swap was not found in the database.");
            }

            dbRecord.Status = SwapStatus.Accepted;
            dbRecord.AcceptedTime = DateTime.UtcNow;
            dbRecord.TimeoutBlockHeight = timeoutBlockHeight;

            if (!dbRecord.IsForward)
            {
                if (lockupAddress is null)
                {
                    this.log.Debug("$<LOCKUP_ADDRESS_MISSING>");
                    throw new DatabaseException("Lockup address is required for reverse swap.");
                }

                dbRecord.LockupAddress = lockupAddress;
            }
            else if (lockupAddress is not null)
            {
                this.log.Debug("$<LOCKUP_ADDRESS_NOT_ALLOWED>");
                throw new DatabaseException("Lockup address is not allowed for forward swap.");
            }

            _ = db.SaveChanges();
            transaction.Commit();

            this.log.Debug($"Swap ID {id} has been marked as accepted in the database.");
        }
        catch (Exception e)
        {
            this.log.Error($"Marking swap ID {id} as accepted in the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Marking swap ID {id} as accepted in the database failed.", e);
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Marks the given swap as rejected in the database.
    /// </summary>
    /// <param name="id">ID of the swap.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task MarkSwapRejectedAsync(long id)
    {
        this.log.Debug($"* {nameof(id)}={id}");

        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps.FindAsync(id).ConfigureAwait(false);
            if (dbRecord is null)
            {
                this.log.Error($"Swap with ID '{id}' was not found in the database.");
                this.log.Debug("$<SWAP_NOT_FOUND>");
                throw new DatabaseException($"Marking the swap with ID '{id}' as rejected in the database failed because the swap was not found in the database.");
            }

            dbRecord.Status = SwapStatus.ProviderErrorNotAccepted;
            dbRecord.FailTime = DateTime.UtcNow;

            _ = db.SaveChanges();
            transaction.Commit();

            this.log.Debug($"Swap ID {id} has been marked as rejected in the database.");
        }
        catch (Exception e)
        {
            this.log.Error($"Marking swap ID {id} as rejected in the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Marking swap ID {id} as rejected in the database failed.", e);
        }

        this.log.Debug("$");
    }
}