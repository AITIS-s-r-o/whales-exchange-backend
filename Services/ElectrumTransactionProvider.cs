using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WhalesExchangeBackend.Services.ElectrumRpc;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Provider of Bitcoin transaction data retrieved from the Electrum client.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI.")]
internal class ElectrumTransactionProvider : IAsyncDisposable
{
    /// <summary>Caching options for cached transactions in <see cref="transactionsCache"/>.</summary>
    private static readonly MemoryCacheEntryOptions cacheEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(5),
    };

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Client that communicates with Electrum RPC server.</summary>
    private readonly ElectrumRpcClient electrumRpcClient;

    /// <summary>Cache of transactions indexed by transaction hashes.</summary>
    private readonly MemoryCache transactionsCache;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="electrumRpcClient">Client that communicates with Electrum RPC server.</param>
    public ElectrumTransactionProvider(ElectrumRpcClient electrumRpcClient)
    {
        this.log.Debug("*");

        this.disposedValueLock = new();
        this.electrumRpcClient = electrumRpcClient;

        MemoryCacheOptions memoryCacheOptions = new();
        this.transactionsCache = new(memoryCacheOptions);

        this.log.Debug("$");
    }

    /// <summary>
    /// Gets transaction by its ID. First, it tries to get the transaction from the cache. If it is not there, it tries to get it from the Electrum client.
    /// </summary>
    /// <param name="transactionId">Bitcoin transaction ID in hex format.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>Transaction data, or <c>null</c> if the transaction ID could not be found or retrieved.</returns>
    public async Task<ElectrumTransactionWithData?> GetTransactionAsync(string transactionId, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(transactionId)}='{transactionId}'");

        if (this.transactionsCache.TryGetValue(transactionId, out ElectrumTransactionWithData? result))
        {
            this.log.Debug($"$<CACHED>='{result}'");
            return result;
        }

        string? transactionData = null;
        try
        {
            transactionData = await this.electrumRpcClient.GetTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false);
        }
        catch (ElectrumRpcException e)
        {
            this.log.Error($"Electrum server reported error when querying transaction ID '{transactionId}': {e}");
        }

        if (transactionData is null)
        {
            this.log.Debug($"$<NO_DATA>='{result}'");
            return null;
        }

        try
        {
            ElectrumTransaction? electrumTransaction = await this.electrumRpcClient.DeserializeAsync(transactionData, cancellationToken).ConfigureAwait(false);
            if (electrumTransaction is not null)
                result = new(electrumTransaction, transactionData);
        }
        catch (Exception e)
        {
            this.log.Error($"Electrum server reported error when deserializing transaction data for transaction ID '{transactionId}': {e}");
        }

        if (result is not null)
        {
            _ = this.transactionsCache.Set(transactionId, result, cacheEntryOptions);
            this.log.Debug($"Transaction ID '{transactionId}' has been added to the cache.");
        }

        this.log.Debug($"$='{result}'");
        return result;
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

        this.log.Debug("Disposing cached transactions.");
        this.transactionsCache.Dispose();

        this.log.Debug("$");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}