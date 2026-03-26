using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Exceptions;
using WhalesExchangeBackend.Services.ElectrumRpc;
using WhalesExchangeBackend.Utils;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Exchange rate service.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class SwapProviderFetcher : System.IAsyncDisposable
{
    /// <summary>Frequnecy with which the fetcher queries the list of providers from Electrum.</summary>
    private static readonly TimeSpan fetchFrequency = TimeSpan.FromMinutes(1);

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Client that communicates with Electrum RPC server.</summary>
    private readonly ElectrumRpcClient electrumRpcClient;

    /// <summary>Provider of access to swap providers and their offers in the database.</summary>
    private readonly SwapProviderRepository swapProviderRepository;

    /// <summary>Cancellation source announcing termination of the instance, i.e. disconnection.</summary>
    private readonly CancellationTokenSource shutdownTokenSource;

    /// <summary>Background task periodically downloading latest exchange rates.</summary>
    private readonly JoinableTask syncTask;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="electrumRpcClient">Client that communicates with Electrum RPC server.</param>
    /// <param name="swapProviderRepository">Provider of access to swap providers and their offers in the database.</param>
    /// <param name="joinableTaskFactory">Factory for starting async tasks running in the background.</param>
    public SwapProviderFetcher(ElectrumRpcClient electrumRpcClient, SwapProviderRepository swapProviderRepository, JoinableTaskFactory joinableTaskFactory)
    {
        this.log.Debug("*");

        this.disposedValueLock = new();
        this.shutdownTokenSource = new();

        this.electrumRpcClient = electrumRpcClient;
        this.swapProviderRepository = swapProviderRepository;

        this.syncTask = joinableTaskFactory.RunAsync(this.SyncLoopAsync, JoinableTaskCreationOptions.LongRunning);

        this.log.Debug("$");
    }

    /// <summary>
    /// Loop that refreshes exchange rates for selected currency pairs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task SyncLoopAsync()
    {
        this.log.Debug("*");

        CancellationToken cancellationToken = this.shutdownTokenSource.Token;

        try
        {
            while (true)
            {
                ElectrumSwapProvider[]? providers = null;
                try
                {
                    providers = await this.electrumRpcClient.GetSubmarineSwapProvidersAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationFailedException ofe)
                {
                    this.log.Error($"Getting list of Electrum submarine swap providers failed with exception: {ofe}");
                }
                catch (ElectrumRpcException ere)
                {
                    this.log.Error($"Electrum server reported error when querying list of submarine swap providers: {ere}");
                }

                if (providers is not null)
                {
                    foreach (ElectrumSwapProvider provider in providers)
                    {
                        DateTime lastSeen = provider.TimestampSec.FromUnixTimeSeconds();
                        bool isNew = await this.swapProviderRepository.UpsertAsync(provider.Pubkey, lastSeen, provider.PoWBits, percentageFeeForward: provider.PercentageFee,
                            percentageFeeReverse: provider.PercentageFee, minAmountForwardSat: provider.MinAmountSat, minAmountReverseSat: provider.MinAmountSat,
                            maxAmountForwardSat: provider.MaxAmountForwardSat, maxAmountReverseSat: provider.MaxAmountReverseSat, miningFeeForwardSat: provider.MiningFeeSat,
                            miningFeeReverseSat: provider.MiningFeeSat).ConfigureAwait(false);

                        if (isNew) this.log.Debug($"New provider pubkey '{provider.Pubkey}' has been added to the database.");
                        else this.log.Debug($"Provider pubkey '{provider.Pubkey}' has been updated in the database.");
                    }
                }

                this.log.Debug($"Wait {fetchFrequency} before next round.");
                await Task.Delay(fetchFrequency, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            this.log.Debug("Shutdown detected.");
        }
        catch (Exception e)
        {
            this.log.Error($"Synchronization loop failed with exception: {e}");
            throw;
        }

        this.log.Debug("$");
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

        this.log.Debug("Signaling shutdown.");
        await this.shutdownTokenSource.CancelAsync().ConfigureAwait(false);

        this.log.Debug("Waiting for the sync task to finish.");
        await this.syncTask.JoinAsync().ConfigureAwait(false);

        this.log.Debug("Disposing shutdown token source.");
        this.shutdownTokenSource.Dispose();

        this.log.Debug("$");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}