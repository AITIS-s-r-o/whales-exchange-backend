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
    /// Inserts or a new forward swap to the database.
    /// </summary>
    /// <param name="frontendId">Frontend ID of the swap.</param>
    /// <param name="providerPubkey">Public key of the swap provider as a hex string.</param>
    /// <param name="userIpAddress">Remote IP address of the user.</param>
    /// <param name="amountToPaySats">Amount the client should pay (including all fees) in satoshis.</param>
    /// <param name="amountToReceiveSats">Amount the client should receive in satoshis.</param>
    /// <param name="refundPublicKeyHex">Public key that will be used for the refund of the on-chain funds if a swap fails in hex format.</param>
    /// <param name="invoice">Lightning invoice that the swap provider will pay.</param>
    /// <param name="paymentHashHex">Payment hash of the lightning invoice in hex format.</param>
    /// <returns>Newly created database record.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    /// <exception cref="OperationFailedException">Thrown when the swap parameters are invalid.</exception>
    internal async Task<DbSwap> InsertForwardAsync(string frontendId, string providerPubkey, string userIpAddress, long amountToPaySats, long amountToReceiveSats,
        string refundPublicKeyHex, string invoice, string paymentHashHex)
    {
        this.log.Debug($"* {nameof(frontendId)}='{frontendId}',{nameof(providerPubkey)}='{providerPubkey}',{nameof(userIpAddress)}='{userIpAddress}',{
            nameof(amountToPaySats)}={amountToPaySats},{nameof(amountToReceiveSats)}={amountToReceiveSats},{nameof(refundPublicKeyHex)}='{refundPublicKeyHex}',{
            nameof(invoice)}='{invoice}',{nameof(paymentHashHex)}='{paymentHashHex}'");

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
                throw new DatabaseException($"Inserting a new forward swap to the database failed because the swap provider with pubkey '{
                    providerPubkey}' was not found in the database.");
            }

            // We do not allow:
            // - more than one incomplete swap with the same refund key, or
            // - more than one swap with the same payment hash.
            DbSwap? existingSwap = await db.Swaps
                .FirstOrDefaultAsync(c => ((c.ClaimPublicKey == refundPublicKeyHex) && (c.Status < SwapStatus.FundingTxSpent))
                    || (c.PaymentHashHex == paymentHashHex))
                .ConfigureAwait(false);
            if (existingSwap is not null)
            {
                string message;

                if (existingSwap.PaymentHashHex == paymentHashHex)
                {
                    this.log.Debug($"Existing swap ID {existingSwap.Id} already used payment hash '{paymentHashHex}'.");
                    message = "Payment hash must not be reused.";
                }
                else
                {
                    this.log.Debug($"Existing swap ID {existingSwap.Id} has not completed yet and is using the refund public key '{refundPublicKeyHex}'.");
                    message = "A swap with the same refund public key already exists. Use a fresh refund address, or wait for the existing swap to complete.";
                }

                this.log.Debug("$<SWAP_EXISTS>");
                throw new OperationFailedException(message);
            }

            DateTime now = DateTime.UtcNow;
            DbSwap dbRecord = new(id: 0, frontendId: frontendId, providerPubkey: providerPubkey, isForward: true, SwapStatus.Created, amountToPaySats: amountToPaySats,
                amountToReceiveSats: amountToReceiveSats, clientAddress: null, lockupAddress: null, lockupOutputIndex: null, fundingTxId: null, timeoutBlockHeight: null,
                createdTime: now, acceptedTime: null, fundingTime: null, spentTime: null, failTime: null, fundingTxData: null, clientTxId: null, clientTxData: null,
                claimPublicKey: refundPublicKeyHex, paymentHashHex: paymentHashHex, redeemScriptHex: null, dbSwapProvider);

            _ = db.Swaps.Add(dbRecord);
            _ = db.SaveChanges();
            transaction.Commit();

            result = dbRecord;
            this.log.Debug($"Reverse swap ID {result.Id} through provider '{providerPubkey}' has been added to the database.");
        }
        catch (OperationFailedException)
        {
            throw;
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
    /// Inserts or a new reverse swap to the database.
    /// </summary>
    /// <param name="frontendId">Frontend ID of the swap.</param>
    /// <param name="providerPubkey">Public key of the swap provider as a hex string.</param>
    /// <param name="userIpAddress">Remote IP address of the user.</param>
    /// <param name="amountToPaySats">Amount the client should pay (including all fees) in satoshis.</param>
    /// <param name="amountToReceiveSats">Amount the client should receive in satoshis.</param>
    /// <param name="claimAddress">Bitcoin address that will be used to claim the on-chain funds.</param>
    /// <param name="claimPublicKey">Public key that will be used to claim the on-chain funds in hex format.</param>
    /// <returns>Newly created database record.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    /// <exception cref="OperationFailedException">Thrown when the swap parameters are invalid.</exception>
    public async Task<DbSwap> InsertReverseAsync(string frontendId, string providerPubkey, string userIpAddress, long amountToPaySats, long amountToReceiveSats,
        string claimAddress, string claimPublicKey)
    {
        this.log.Debug($"* {nameof(frontendId)}='{frontendId}',{nameof(providerPubkey)}='{providerPubkey}',{nameof(userIpAddress)}='{userIpAddress}',{
            nameof(amountToPaySats)}={amountToPaySats},{nameof(amountToReceiveSats)}={amountToReceiveSats},{nameof(claimAddress)}='{claimAddress}',{
            nameof(claimPublicKey)}='{claimPublicKey}'");

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

            // We do not allow:
            // - more than one incomplete swap with the same claim address, or
            // - more than one swap with the same claim public key.
            DbSwap? existingSwap = await db.Swaps
                .FirstOrDefaultAsync(c => ((c.ClientAddress == claimAddress) && (c.Status < SwapStatus.FundingTxSpent))
                    || (c.ClaimPublicKey == claimPublicKey))
                .ConfigureAwait(false);
            if (existingSwap is not null)
            {
                string message;

                if (existingSwap.ClaimPublicKey == claimPublicKey)
                {
                    this.log.Debug($"Existing swap ID {existingSwap.Id} already used claim public key '{claimPublicKey}'.");
                    message = "Claim public key must not be reused. Avoid creating multiple new swaps in parallel from the same browser.";
                }
                else
                {
                    this.log.Debug($"Existing swap ID {existingSwap.Id} has not completed yet and is using the claim address '{claimAddress}'.");
                    message = "A swap with the same destination already exists. Use a fresh address, or wait for the existing swap to complete.";
                }

                this.log.Debug("$<SWAP_EXISTS>");
                throw new OperationFailedException(message);
            }

            DateTime now = DateTime.UtcNow;
            DbSwap dbRecord = new(id: 0, frontendId: frontendId, providerPubkey: providerPubkey, isForward: false, SwapStatus.Created, amountToPaySats: amountToPaySats,
                amountToReceiveSats: amountToReceiveSats, clientAddress: claimAddress, lockupAddress: null, lockupOutputIndex: null, fundingTxId: null, timeoutBlockHeight: null,
                createdTime: now, acceptedTime: null, fundingTime: null, spentTime: null, failTime: null, fundingTxData: null, clientTxId: null, clientTxData: null,
                claimPublicKey: claimPublicKey, paymentHashHex: null, redeemScriptHex: null, dbSwapProvider);

            _ = db.Swaps.Add(dbRecord);
            _ = db.SaveChanges();
            transaction.Commit();

            result = dbRecord;
            this.log.Debug($"Reverse swap ID {result.Id} through provider '{providerPubkey}' has been added to the database.");
        }
        catch (OperationFailedException)
        {
            throw;
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
    /// Marks the given swap as accepted in the database and sets the lockup address and redeem script, if applicable.
    /// </summary>
    /// <param name="id">ID of the swap.</param>
    /// <param name="lockupAddress">Lockup address for the reverse swap.</param>
    /// <param name="timeoutBlockHeight">Block height after which the swap is considered expired.</param>
    /// <param name="redeemScriptHex">Redeem script of the swap in hex format, or <c>null</c> for reverse swaps.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task MarkSwapAcceptedAsync(long id, string lockupAddress, long timeoutBlockHeight, string? redeemScriptHex)
    {
        this.log.Debug($"* {nameof(id)}={id},{nameof(lockupAddress)}='{lockupAddress}',{nameof(timeoutBlockHeight)}={timeoutBlockHeight},{
            nameof(redeemScriptHex)}='{redeemScriptHex}'");

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
            dbRecord.LockupAddress = lockupAddress;
            dbRecord.RedeemScriptHex = redeemScriptHex;

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

    /// <summary>
    /// Marks the swap as cancelled by the client in the database.
    /// </summary>
    /// <param name="frontendId">Frontend ID of the swap.</param>
    /// <param name="maximumStatus">Maximum status that the swap can have to be cancelled.</param>
    /// <returns><c>true</c> if the swap record was cancelled in the database, <c>false</c> otherwise.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<bool> MarkClientCancelledAsync(string frontendId, SwapStatus maximumStatus)
    {
        this.log.Debug($"* {nameof(frontendId)}='{frontendId}',{nameof(maximumStatus)}={maximumStatus}");

        bool result = false;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps
                .Where(c => c.FrontendId == frontendId)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);

            if (dbRecord is not null)
            {
                if (dbRecord.Status <= maximumStatus)
                {
                    dbRecord.Status = SwapStatus.ClientCancelled;
                    _ = db.SaveChanges();
                    transaction.Commit();

                    this.log.Debug($"Swap with frontend ID '{frontendId}' has been marked as cancelled in the database.");
                    result = true;
                }
                else this.log.Debug($"Swap with frontend ID '{frontendId}' has status {dbRecord.Status} and cannot be cancelled.");
            }
            else this.log.Debug($"Swap with frontend ID '{frontendId}' was not found in the database.");
        }
        catch (Exception e)
        {
            this.log.Error($"Marking swap with the frontend ID '{frontendId}' as cancelled in the database failed with exception: {e}");
            this.log.Debug("$<DB_EXCEPTION>");
            throw new DatabaseException($"Marking swap with the frontend ID '{frontendId}' as cancelled in the database failed.", e);
        }

        this.log.Debug($"$={result}");
        return result;
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
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(isConfirmed)}={isConfirmed},{nameof(transactionId)}='{transactionId}',{nameof(outputIndex)}={outputIndex},{
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
                // In case of a forward swap, the funding on-chain Bitcoin transaction is created by the client, so if it is not created within the expected time, it is a client
                // error. In case of a reverse swap, the funding on-chain Bitcoin transaction is created by the swap provider, so if it is not created within the expected time,
                // it is a provider error.
                //
                // In case of a forward swap, claiming the funding transaction output is the responsibility of the swap provider, but the fault cannot be assigned to the provider
                // because it may also be the case that the client did not reveal the preimage by completing the lightning payment, or it could be the case that the provider did
                // not even initiate this lightning payment. In case of a reverse swap, claiming the funding transaction output is the responsibility of the client, so if it is
                // not claimed within the expected time, it is a client error.
                SwapStatus newStatus = dbRecord.IsForward
                    ? (isFundingTransaction ? SwapStatus.ClientErrorFundingTxNotCreated : SwapStatus.ErrorFundingTxNotSpent)
                    : (isFundingTransaction ? SwapStatus.ErrorFundingTxNotCreated : SwapStatus.ClientErrorFundingTxNotSpent);

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

                this.log.Debug($"Changing status of swap ID {swapId} to {dbRecord.Status}. Fail time set to {dbRecord.FailTime}.");
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
    /// <param name="isClaimed"><c>true</c> if the swap was claimed, <c>false</c> if it was refunded.</param>
    /// <returns>Update swap database record, or <c>null</c> if the swap ID was not found in the database.</returns>
    /// <exception cref="DatabaseException">Thrown when the database operation fails.</exception>
    public async Task<DbSwap?> SwapClaimedOrRefundedAsync(long swapId, string transactionId, string transactionData, bool isClaimed)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(transactionId)}='{transactionId}',{nameof(transactionData)}='{transactionData.ToBoundedString()}',{
            nameof(isClaimed)}={isClaimed}");

        DbSwap? result = null;
        try
        {
            using ApplicationDbContext db = this.dbContextFactory.CreateDbContext();
            using IDisposable dbLocked = await this.dbLock.EnterAsync().ConfigureAwait(false);
            using IDbContextTransaction transaction = db.BeginTransaction();

            DbSwap? dbRecord = await db.Swaps.FindAsync(swapId).ConfigureAwait(false);
            if (dbRecord is not null)
            {
                if (isClaimed)
                {
                    if (dbRecord.Status != SwapStatus.FundingTxConfirmed)
                    {
                        throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.FundingTxSpent} requires the swap status to be in {
                            SwapStatus.FundingTxConfirmed} status, but its status is {dbRecord.Status}.");
                    }
                }
                else
                {
                    if (dbRecord.Status != SwapStatus.ErrorFundingTxNotSpent)
                    {
                        throw new SanityCheckException($"Changing status of swap ID {swapId} to {SwapStatus.FundingTxRefunded} requires the swap status to be in {
                            SwapStatus.ErrorFundingTxNotSpent} status, but its status is {dbRecord.Status}.");
                    }
                }

                dbRecord.ClientTxId = transactionId;
                dbRecord.ClientTxData = transactionData;

                dbRecord.SpentTime = DateTime.UtcNow;
                dbRecord.Status = isClaimed ? SwapStatus.FundingTxSpent : SwapStatus.FundingTxRefunded;

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