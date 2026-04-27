using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesExchangeBackend.SharedLib.Helpers;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Data.Repository;

/// <summary>
/// Provider of access to swap providers and their offers in the database.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class SwapProviderRepository : RepositoryBase
{
    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="dbContextFactory">Factory for the database context.</param>
    /// <param name="dbLocks">Collection of database repository locks.</param>
    public SwapProviderRepository(ApplicationDbContextFactory dbContextFactory, DbLocks dbLocks) :
        base(dbContextFactory, dbLocks, nameof(SwapProviderRepository))
    {
        this.log.Debug("*$");
    }

    /// <summary>
    /// Inserts or updates a swap provider in the database.
    /// </summary>
    /// <param name="pubkey">Public key of the swap provider as a hex string.</param>
    /// <param name="lastSeen">UTC time when the provider was last seen.</param>
    /// <param name="poWBits">Amount of PoW the provider used for its profile.</param>
    /// <param name="percentageFeeForward">Forward swap provider fee in percent.</param>
    /// <param name="percentageFeeReverse">Reverse swap provider fee in percent.</param>
    /// <param name="minAmountForwardSat">Minimum amount for a forward swap in satoshis.</param>
    /// <param name="minAmountReverseSat">Minimum amount for a reverse swap in satoshis.</param>
    /// <param name="maxAmountForwardSat">Maximum amount for a forward swap in satoshis.</param>
    /// <param name="maxAmountReverseSat">Maximum amount for a reverse swap in satoshis.</param>
    /// <param name="miningFeeForwardSat">Mining fee for forward swaps in satoshis.</param>
    /// <param name="miningFeeReverseSat">Mining fee for reverse swaps in satoshis.</param>
    /// <param name="serverStartTime">UTC timestamp when the current backend instance started.</param>
    /// <param name="capabilities">List of capabilities supported by the swap provider.</param>
    /// <returns><c>true</c> if a new record was inserted in the database, <c>false</c> if an existing record has been updated.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<bool> UpsertAsync(string pubkey, DateTime lastSeen, int poWBits, decimal percentageFeeForward, decimal percentageFeeReverse, long minAmountForwardSat,
        long minAmountReverseSat, long maxAmountForwardSat, long maxAmountReverseSat, long miningFeeForwardSat, long miningFeeReverseSat, DateTime serverStartTime,
        string[] capabilities)
    {
        this.log.Debug($"* {nameof(pubkey)}='{pubkey}',{nameof(lastSeen)}={lastSeen},{nameof(poWBits)}={poWBits},{nameof(percentageFeeForward)}={percentageFeeForward},{
            nameof(percentageFeeReverse)}={percentageFeeReverse},{nameof(minAmountForwardSat)}={minAmountForwardSat},{nameof(minAmountReverseSat)}={minAmountReverseSat},{
            nameof(maxAmountForwardSat)}={maxAmountForwardSat},{nameof(maxAmountReverseSat)}={maxAmountReverseSat},{nameof(miningFeeForwardSat)}={miningFeeForwardSat},{
            nameof(minAmountReverseSat)}={minAmountReverseSat},{nameof(serverStartTime)}={serverStartTime},{nameof(capabilities)}={capabilities.LogJoin()}");

        bool result;
        string capStr = string.Join(',', capabilities);

        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwapProvider? dbRecord = await db.SwapProviders.FindAsync(pubkey).ConfigureAwait(false);
            if (dbRecord is null)
            {
                DateTime now = DateTime.UtcNow;

                dbRecord = new(pubkey, firstSeen: now, lastSeen: lastSeen, poWBits: poWBits, percentageFeeForward: percentageFeeForward, percentageFeeReverse: percentageFeeReverse,
                    minAmountForwardSat: minAmountForwardSat, minAmountReverseSat: minAmountReverseSat, maxAmountForwardSat: maxAmountForwardSat,
                    maxAmountReverseSat: maxAmountReverseSat, miningFeeForwardSat: miningFeeForwardSat, miningFeeReverseSat: miningFeeReverseSat, slotsPresent: 1, slotsMissed: 0,
                    capabilities: capStr);

                _ = db.SwapProviders.Add(dbRecord);
                result = true;
            }
            else
            {
                dbRecord.PoWBits = poWBits;
                dbRecord.PercentageFeeForward = percentageFeeForward;
                dbRecord.PercentageFeeReverse = percentageFeeReverse;
                dbRecord.MinAmountForwardSat = minAmountForwardSat;
                dbRecord.MinAmountReverseSat = minAmountReverseSat;
                dbRecord.MaxAmountForwardSat = maxAmountForwardSat;
                dbRecord.MaxAmountReverseSat = maxAmountReverseSat;
                dbRecord.MiningFeeForwardSat = miningFeeForwardSat;
                dbRecord.MiningFeeReverseSat = miningFeeReverseSat;
                dbRecord.SlotsPresent += ProviderPresenceCalculator.CalculatePresentSlots(prevLastSeen: dbRecord.LastSeen, newLastSeen: lastSeen, serverStartTime: serverStartTime);
                dbRecord.SlotsMissed += ProviderPresenceCalculator.CalculateMissedSlots(prevLastSeen: dbRecord.LastSeen, newLastSeen: lastSeen, serverStartTime: serverStartTime);
                dbRecord.LastSeen = lastSeen;
                dbRecord.Capabilities = capStr;

                _ = db.SwapProviders.Update(dbRecord);
                result = false;
            }

            _ = db.SaveChanges();
            transaction.Commit();
        }
        catch (Exception e)
        {
            this.log.Error($"Upserting swap provider pubkey '{pubkey}' in the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Upserting swap provider pubkey '{pubkey}' in the database failed.", e);
        }

        this.log.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Gets list of swap providers that has been seen recently.
    /// </summary>
    /// <param name="lastSeenLimit">UTC timestamp after which all the returned providers must have been seen.</param>
    /// <returns>Returns a list of swap providers.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwapProvider[]> GetRecentAsync(DateTime lastSeenLimit)
    {
        this.log.Debug($"* {nameof(lastSeenLimit)}={lastSeenLimit}");

        DbSwapProvider[] result;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);

            result = await db.SwapProviders
                .Where(c => c.LastSeen > lastSeenLimit)
                .OrderBy(c => c.PercentageFeeReverse)
                .ThenBy(c => c.Pubkey)
                .ToArrayAsync()
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this.log.Error($"Getting list of recent swap providers from the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException("Getting list of swap providers from the database failed.", e);
        }

        this.log.Debug($"|$|={result.Length}");
        return result;
    }

    /// <summary>
    /// Gets a swap providers by its pubkey.
    /// </summary>
    /// <param name="pubkey">Public key of a swap provider.</param>
    /// <returns>Returns the given swap provider, or <c>null</c> if the given pubkey was not found.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwapProvider?> GetByPubkeyAsync(string pubkey)
    {
        this.log.Debug($"* {nameof(pubkey)}='{pubkey}'");

        DbSwapProvider? result;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);

            result = await db.SwapProviders
                .FindAsync(pubkey)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this.log.Error($"Getting swap provider with pubkey '{pubkey}' failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Getting swap provider with pubkey '{pubkey}' failed.", e);
        }

        this.log.Debug($"$='{result}'");
        return result;
    }
}