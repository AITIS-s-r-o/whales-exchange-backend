using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesExchangeBackend.SharedLib.Helpers;
using WhalesExchangeBackend.SharedLib.Models;
using WhalesSecret.TradeScriptLib.Exceptions;
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
    /// <returns><c>true</c> if a new record was inserted in the database, <c>false</c> if an existing record has been updated.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<bool> UpsertAsync(string pubkey, DateTime lastSeen, int poWBits, decimal percentageFeeForward, decimal percentageFeeReverse, long minAmountForwardSat,
        long minAmountReverseSat, long maxAmountForwardSat, long maxAmountReverseSat, long miningFeeForwardSat, long miningFeeReverseSat, DateTime serverStartTime)
    {
        this.log.Debug($"* {nameof(pubkey)}='{pubkey}',{nameof(lastSeen)}={lastSeen},{nameof(poWBits)}={poWBits},{nameof(percentageFeeForward)}={percentageFeeForward},{
            nameof(percentageFeeReverse)}={percentageFeeReverse},{nameof(minAmountForwardSat)}={minAmountForwardSat},{nameof(minAmountReverseSat)}={minAmountReverseSat},{
            nameof(maxAmountForwardSat)}={maxAmountForwardSat},{nameof(maxAmountReverseSat)}={maxAmountReverseSat},{nameof(miningFeeForwardSat)}={miningFeeForwardSat},{
            nameof(minAmountReverseSat)}={minAmountReverseSat},{nameof(serverStartTime)}={serverStartTime}");

        bool result;
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
                    maxAmountReverseSat: maxAmountReverseSat, miningFeeForwardSat: miningFeeForwardSat, miningFeeReverseSat: miningFeeReverseSat, slotsPresent: 1, slotsMissed: 0);

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

    /// <summary>
    /// Update information about the swap's funding Bitcoin transaction in the database.
    /// </summary>
    /// <param name="swapId">ID of the swap.</param>
    /// <param name="isConfirmed"><c>true</c> if the transaction was confirmed sufficiently, <c>false</c> if it has been only seen in a mempool.</param>
    /// <param name="transactionId">Transaction ID of the funding transaction in hex format.</param>
    /// <param name="transactionData">Raw transaction data in hex format, or <c>null</c> if not available.</param>
    /// <returns>Update swap database record, or <c>null</c> if the swap ID was not found in the database.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap?> FundingTransactionSetAsync(long swapId, bool isConfirmed, string transactionId, string? transactionData)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(isConfirmed)}={isConfirmed},{nameof(transactionId)}='{transactionId}',{
            nameof(transactionData)}='{transactionData.ToBoundedString()}'");

        DbSwap? result = null;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps.FindAsync(swapId).ConfigureAwait(false);
            if (dbRecord is not null)
            {
                if (!isConfirmed && (dbRecord.Status != SwapStatus.Accepted))
                {
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.FundingTxCreated} requires the swap status to be in {
                        SwapStatus.Accepted} status, but its status is {dbRecord.Status}.");
                }

                if (isConfirmed && (dbRecord.Status != SwapStatus.Accepted) && (dbRecord.Status != SwapStatus.FundingTxCreated))
                {
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.FundingTxConfirmed} requires the swap status to be either in {
                        SwapStatus.Accepted} or {SwapStatus.FundingTxCreated} status, but its status is {dbRecord.Status}.");
                }

                dbRecord.FundingTxId = transactionId;
                dbRecord.FundingTxData = transactionData;

                if (dbRecord.FundingTime is null)
                    dbRecord.FundingTime = DateTime.UtcNow;

                dbRecord.Status = isConfirmed ? SwapStatus.FundingTxConfirmed : SwapStatus.FundingTxCreated;

                _ = db.Swaps.Update(dbRecord);

                _ = db.SaveChanges();
                transaction.Commit();

                this.log.Debug($"Swap ID {swapId} status changed to {dbRecord.Status}. Funding time set to {dbRecord.FundingTime}.");
                result = dbRecord;
            }
            else this.log.Error($"Swap ID {swapId} has not been found in the database.");
        }
        catch (Exception e)
        {
            this.log.Error($"Updating swap ID {swapId} in the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Updating swap ID {swapId} in the database failed.", e);
        }

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// Mark the swap's funding Bitcoin transaction as expired in the database.
    /// </summary>
    /// <param name="swapId">ID of the swap.</param>
    /// <returns>Update swap database record, or <c>null</c> if the swap ID was not found in the database.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap?> FundingTransactionTimeoutAsync(long swapId)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId}");

        DbSwap? result = null;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps.FindAsync(swapId).ConfigureAwait(false);
            if (dbRecord is not null)
            {
                if (dbRecord.Status != SwapStatus.Accepted)
                {
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.ErrorFundingTxNotCreated} requires the swap status to be in {
                        SwapStatus.Accepted} status, but its status is {dbRecord.Status}.");
                }

                dbRecord.FailTime = DateTime.UtcNow;
                dbRecord.Status = SwapStatus.ErrorFundingTxNotCreated;

                _ = db.Swaps.Update(dbRecord);

                _ = db.SaveChanges();
                transaction.Commit();

                this.log.Debug($"Swap ID status changed to {dbRecord.Status}. Fail time set to {dbRecord.FailTime}.");
                result = dbRecord;
            }
            else this.log.Error($"Swap ID {swapId} has not been found in the database.");
        }
        catch (Exception e)
        {
            this.log.Error($"Updating swap ID {swapId} in the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Updating swap ID {swapId} in the database failed.", e);
        }

        this.log.Debug($"$='{result}'");
        return result;
    }
}