using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesExchangeBackend.SharedLib.Models;
using WhalesExchangeBackend.Utils;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Data.Repository;

/// <inheritdoc cref="ISwapRepository"/>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class SwapRepository : RepositoryBase, ISwapRepository
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
    /// <param name="claimAddress">Bitcoin address that will be used to claim the on-chain funds.</param>
    /// <returns>Newly created database record.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap> InsertReverseAsync(string providerPubkey, long amountToPaySats, long amountToReceiveSats, string claimAddress)
    {
        this.log.Debug($"* {nameof(providerPubkey)}='{providerPubkey}',{nameof(amountToPaySats)}={amountToPaySats},{nameof(amountToReceiveSats)}={amountToReceiveSats},{
            nameof(claimAddress)}='{claimAddress}'");

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
                amountToReceiveSats: amountToReceiveSats, clientAddress: claimAddress, lockupAddress: null, lockupOutputIndex: null, fundingTxId: null, timeoutBlockHeight: null,
                createdTime: now, acceptedTime: null, fundingTime: null, spentTime: null, failTime: null, fundingTxData: null, clientTxId: null, clientTxData: null,
                dbSwapProvider);

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

    /// <inheritdoc/>
    public async Task<DbSwap?[]> GetSwapsByFrontendIdsAsync(string[] frontendIds)
    {
        this.log.Debug($"* {nameof(frontendIds)}={frontendIds.LogJoin()}");

        DbSwap?[] result;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);

            Dictionary<string, DbSwap> dbRecordsMap = await db.Swaps
                .Where(i => frontendIds.Contains(i.FrontendId))
                .ToDictionaryAsync(i => i.FrontendId)
                .ConfigureAwait(false);

            result = new DbSwap?[frontendIds.Length];
            for (int i = 0; i < frontendIds.Length; i++)
            {
                string frontendId = frontendIds[i];
                if (dbRecordsMap.TryGetValue(frontendId, out DbSwap? dbRecord))
                    result[i] = dbRecord;
            }
        }
        catch (Exception e)
        {
            this.log.Error($"Getting {frontendIds.Length} swaps from the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Getting {frontendIds.Length} swaps from the database failed.", e);
        }

        this.log.Debug($"|$|={result.Length}");
        return result;
    }

    /// <summary>
    /// Gets swaps from the database.
    /// </summary>
    /// <param name="id">ID of the swap to retrieve.</param>
    /// <returns>Requested swap, or <c>null</c> if the ID is not found in the database.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap?> GetSwapByIdAsync(long id)
    {
        this.log.Debug($"* {nameof(id)}={id}");

        DbSwap? result;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);

            result = await db.Swaps.FindAsync(id).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this.log.Error($"Getting swap with ID {id} from the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Getting swap with ID {id} from the database failed.", e);
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
    /// <param name="outputIndex">Output index of the funding transaction output that holds the swapped funds.</param>
    /// <param name="transactionData">Raw transaction data in hex format, or <c>null</c> if not available.</param>
    /// <returns>Update swap database record, or <c>null</c> if the swap ID was not found in the database.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap?> FundingTransactionSetAsync(long swapId, bool isConfirmed, string transactionId, int outputIndex, string? transactionData)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(isConfirmed)}={isConfirmed},{nameof(transactionId)}='{transactionId}',{nameof(outputIndex)}={outputIndex},{nameof(transactionData)}='{transactionData.ToBoundedString()}'");

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
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.FundingTxCreated} requires the swap status to be in {SwapStatus.Accepted} status, but its status is {dbRecord.Status}.");
                }

                if (isConfirmed && (dbRecord.Status != SwapStatus.Accepted) && (dbRecord.Status != SwapStatus.FundingTxCreated))
                {
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.FundingTxConfirmed} requires the swap status to be either in {SwapStatus.Accepted} or {SwapStatus.FundingTxCreated} status, but its status is {dbRecord.Status}.");
                }

                dbRecord.FundingTxId = transactionId;
                dbRecord.LockupOutputIndex = outputIndex;
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
    /// <param name="isFundingTransaction"><c>true</c> if the timeout is for a funding transaction, <c>false</c> if it's for a claim transaction.</param>
    /// <returns>Update swap database record, or <c>null</c> if the swap ID was not found in the database.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap?> FundingOrClaimTransactionTimeoutAsync(long swapId, bool isFundingTransaction)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(isFundingTransaction)}={isFundingTransaction}");

        DbSwap? result = null;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps.FindAsync(swapId).ConfigureAwait(false);
            if (dbRecord is not null)
            {
                SwapStatus newStatus = isFundingTransaction ? SwapStatus.ErrorFundingTxNotCreated : SwapStatus.ClientErrorFundingTxNotSpent;
                if (isFundingTransaction && (dbRecord.Status != SwapStatus.Accepted))
                {
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {newStatus} requires the swap status to be in {
                        SwapStatus.Accepted} status, but its status is {dbRecord.Status}.");
                }
                else if (!isFundingTransaction && (dbRecord.Status != SwapStatus.FundingTxCreated) && (dbRecord.Status != SwapStatus.FundingTxConfirmed))
                {
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {newStatus} requires the swap status to be either in {
                        SwapStatus.FundingTxCreated} or {SwapStatus.FundingTxConfirmed} status, but its status is {dbRecord.Status}.");
                }

                dbRecord.FailTime = DateTime.UtcNow;
                dbRecord.Status = newStatus;

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

    /// <summary>
    /// Update information about the swap that was claimed in the database.
    /// </summary>
    /// <param name="swapId">ID of the swap.</param>
    /// <param name="transactionId">Transaction ID of the claim transaction in hex format.</param>
    /// <param name="transactionData">Raw transaction data in hex format, or <c>null</c> if not available.</param>
    /// <returns>Update swap database record, or <c>null</c> if the swap ID was not found in the database.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap?> ReverseSwapClaimedAsync(long swapId, string transactionId, string transactionData)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(transactionId)}='{transactionId}',{nameof(transactionData)}='{transactionData.ToBoundedString()}'");

        DbSwap? result = null;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps.FindAsync(swapId).ConfigureAwait(false);
            if (dbRecord is not null)
            {
                if (dbRecord.Status != SwapStatus.FundingTxConfirmed)
                {
                    throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.FundingTxSpent} requires the swap status to be in {
                        SwapStatus.FundingTxConfirmed} status, but its status is {dbRecord.Status}.");
                }

                dbRecord.ClientTxId = transactionId;
                dbRecord.ClientTxData = transactionData;

                dbRecord.SpentTime = DateTime.UtcNow;
                dbRecord.Status = SwapStatus.FundingTxSpent;

                _ = db.Swaps.Update(dbRecord);

                _ = db.SaveChanges();
                transaction.Commit();

                this.log.Debug($"Swap ID {swapId} status changed to {dbRecord.Status}. Spent time set to {dbRecord.SpentTime}.");
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