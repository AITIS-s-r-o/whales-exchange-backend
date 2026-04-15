using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Services.DataProvider;
using WhalesExchangeBackend.Services.ElectrumRpc;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Provider of blockchain data fetched from the Electrum backend client.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class BlockchainDataProvider : System.IAsyncDisposable
{
    /// <summary>Frequency with which the information about the Electrum blockchain height is updated.</summary>
    private static readonly TimeSpan blockchainHeightUpdateFrequency = TimeSpan.FromSeconds(63);

    /// <summary>Frequency with which the monitored addresses are checked for matching transactions.</summary>
    private static readonly TimeSpan monitoredAddressTransactionUpdateFrequency = TimeSpan.FromSeconds(13);

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Client that communicates with Electrum RPC server.</summary>
    private readonly ElectrumRpcClient electrumRpcClient;

    /// <summary>Cancellation source announcing termination of the instance, i.e. disconnection.</summary>
    private readonly CancellationTokenSource shutdownTokenSource;

    /// <summary>Background task that checks for and processes updates to blockchain height.</summary>
    private readonly JoinableTask blockHeightSyncTask;

    /// <summary>Background task that checks for and processes matches to monitored addresses.</summary>
    private readonly JoinableTask transactionMonitorTask;

    /// <summary>List of subscribed callbacks to be called when a monitored address action occurs.</summary>
    /// <remarks>All access has to be protected by <see cref="dataLock"/>.</remarks>
    private readonly List<OnMonitoredAddressActionCallback> onMonitoredAddressActions;

    /// <summary>Set of monitored Bitcoin addresses.</summary>
    /// <remarks>All access has to be protected by <see cref="dataLock"/>.</remarks>
    private readonly HashSet<MonitoredAddress> monitoredAddresses;

    /// <summary>
    /// Lock object to be used when accessing <see cref="blockchainHeight"/>, <see cref="onMonitoredAddressActions"/>, and <see cref="monitoredAddresses"/>.
    /// </summary>
    private readonly Lock dataLock;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>Current blockchain height.</summary>
    /// <remarks>All access has to be protected by <see cref="dataLock"/>.</remarks>
    private int blockchainHeight;

    /// <summary>
    /// Callback method to be called when a monitored address action occurs.
    /// </summary>
    /// <param name="action">Action that occurred on the monitored address.</param>
    /// <param name="monitoredAddress">Monitored address that triggered the action.</param>
    /// <param name="transactionId">Bitcoin transaction ID in hex format, or <c>null</c> if <paramref name="action"/> is <see cref="MonitoredAddressAction.Timeout"/>.</param>
    /// <param name="transactionData">Raw transaction data in hex format, or <c>null</c> if <paramref name="action"/> is <see cref="MonitoredAddressAction.Timeout"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public delegate Task OnMonitoredAddressActionCallback(MonitoredAddressAction action, MonitoredAddress monitoredAddress, string? transactionId, string? transactionData);

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="electrumRpcClient">Client that communicates with Electrum RPC server.</param>
    /// <param name="joinableTaskFactory">Factory for starting async tasks running in the background.</param>
    public BlockchainDataProvider(ElectrumRpcClient electrumRpcClient, JoinableTaskFactory joinableTaskFactory)
    {
        this.log.Debug("*");

        this.dataLock = new();
        this.disposedValueLock = new();
        this.shutdownTokenSource = new();

        this.electrumRpcClient = electrumRpcClient;

        this.onMonitoredAddressActions = new();
        this.monitoredAddresses = new();

        this.blockHeightSyncTask = joinableTaskFactory.RunAsync(this.BlockHeightSyncLoopAsync, JoinableTaskCreationOptions.LongRunning);
        this.transactionMonitorTask = joinableTaskFactory.RunAsync(this.TransactionMonitorLoopAsync, JoinableTaskCreationOptions.LongRunning);

        this.log.Debug("$");
    }

    /// <summary>
    /// Loop that checks for and processes updates to blockchain height.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task BlockHeightSyncLoopAsync()
    {
        this.log.Debug("*");

        // Yield the thread to avoid blocking the caller by putting this execution in the background.
        await Task.Yield().ConfigureAwait(false);

        CancellationToken cancellationToken = this.shutdownTokenSource.Token;

        try
        {
            while (true)
            {
                ElectrumGetInfoResponse? response = null;
                try
                {
                    response = await this.electrumRpcClient.GetInfoAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationFailedException e)
                {
                    this.log.Error($"Getting blockchain info failed with exception: {e}");
                }
                catch (ElectrumRpcException e)
                {
                    this.log.Error($"Electrum server reported error when querying blockchain info: {e}");
                }

                if (response is not null)
                {
                    if (response.Connected)
                    {
                        if (response.BlockchainHeight == response.ServerHeight)
                        {
                            this.log.Debug($"Electrum server is synced at blockchain height {response.BlockchainHeight}.");

                            OnMonitoredAddressActionCallback[] callbacks = Array.Empty<OnMonitoredAddressActionCallback>();
                            IReadOnlyList<MonitoredAddress> expiredMonitoredAddresses = Array.Empty<MonitoredAddress>();
                            lock (this.dataLock)
                            {
                                if (this.blockchainHeight != response.BlockchainHeight)
                                {
                                    this.blockchainHeight = response.BlockchainHeight;
                                    expiredMonitoredAddresses = this.ProcessNewBlockchainHeightLocked(this.blockchainHeight);
                                    if (expiredMonitoredAddresses.Count > 0)
                                        callbacks = this.onMonitoredAddressActions.ToArray();
                                }
                            }

                            this.log.Debug($"{expiredMonitoredAddresses.Count} monitored addresses timed out, {callbacks.Length} will be called.");

                            foreach (MonitoredAddress monitoredAddress in expiredMonitoredAddresses)
                            {
                                foreach (OnMonitoredAddressActionCallback callback in callbacks)
                                    await callback(MonitoredAddressAction.Timeout, monitoredAddress, transactionId: null, transactionData: null).ConfigureAwait(false);
                            }
                        }
                        else this.log.Debug($"Electrum server is at blockchain height {response.BlockchainHeight}, not synced with its server at {response.ServerHeight}.");
                    }
                    else this.log.Debug("Electrum server is not connected.");
                }

                this.log.Debug($"Wait {blockchainHeightUpdateFrequency} before next round.");
                await Task.Delay(blockchainHeightUpdateFrequency, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            this.log.Debug("Shutdown detected.");
        }
        catch (Exception e)
        {
            this.log.Error($"Blockchain height sync loop failed with exception: {e}");
            throw;
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Loop that checks for and processes matches to monitored addresses.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task TransactionMonitorLoopAsync()
    {
        this.log.Debug("*");

        // Yield the thread to avoid blocking the caller by putting this execution in the background.
        await Task.Yield().ConfigureAwait(false);

        CancellationToken cancellationToken = this.shutdownTokenSource.Token;

        try
        {
            while (true)
            {
                int currentBlockHeight;
                MonitoredAddress[] monitoredAddressesCopy;
                lock (this.dataLock)
                {
                    currentBlockHeight = this.blockchainHeight;
                    monitoredAddressesCopy = this.monitoredAddresses.ToArray();
                }

                List<MonitoredAddress> monitoredAddressesToRemove = new();
                foreach (MonitoredAddress monitoredAddress in monitoredAddressesCopy)
                {
                    ElectrumGetAddressUnspentResponse? response = null;
                    try
                    {
                        response = await this.electrumRpcClient.GetAddressUnspentAsync(monitoredAddress.Address, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationFailedException e)
                    {
                        this.log.Error($"Getting information about monitored address '{monitoredAddress.Address}' failed with exception: {e}");
                    }
                    catch (ElectrumRpcException e)
                    {
                        this.log.Error($"Electrum server reported error when querying information for monitored address '{monitoredAddress.Address}': {e}");
                    }

                    if ((response is not null) && (response.Count > 0))
                    {
                        // Electrum returns unspent outputs sorted by block height in ascending order, so the last one is the newest. However, if the transaction is still in its mempool,
                        // the block height is returned as 0. We can ignore all records with block height prior to monitoring start.
                        if ((response[^1].BlockHeight == 0) || (response[^1].BlockHeight > monitoredAddress.MonitoringStartedAtHeight))
                        {
                            OnMonitoredAddressActionCallback[] callbacks = Array.Empty<OnMonitoredAddressActionCallback>();
                            lock (this.dataLock)
                            {
                                callbacks = this.onMonitoredAddressActions.ToArray();
                            }

                            for (int i = response.Count - 1; i >= 0; i--)
                            {
                                AddressUnspentInfo unspentInfo = response[i];

                                if ((unspentInfo.BlockHeight != 0) && (unspentInfo.BlockHeight < monitoredAddress.MonitoringStartedAtHeight))
                                    break;

                                MonitoredAddressAction? action = null;
                                if (unspentInfo.AmountSats >= monitoredAddress.AmountSats)
                                {
                                    if (unspentInfo.BlockHeight == 0)
                                    {
                                        this.log.Debug($"Monitored address '{monitoredAddress.Address}' received a new unspent output with amount {
                                            unspentInfo.AmountSats} satoshis and it has no confirmation yet.");

                                        action = MonitoredAddressAction.InMempool;
                                    }
                                    else
                                    {
                                        int confirmations = currentBlockHeight - unspentInfo.BlockHeight + 1;
                                        this.log.Debug($"Monitored address '{monitoredAddress.Address}' received a new unspent output with amount {
                                            unspentInfo.AmountSats} satoshis at blockchain height {unspentInfo.BlockHeight}. Current height is {currentBlockHeight}, so the output has {
                                            confirmations}/{monitoredAddress.RequiredConfirmations} confirmations.");

                                        if (confirmations >= monitoredAddress.RequiredConfirmations)
                                            action = MonitoredAddressAction.Confirmed;
                                    }
                                }
                                else
                                {
                                    this.log.Debug($"Value of unspent output '{unspentInfo.TransactionHash}:{unspentInfo.OutputIndex}' is {unspentInfo.AmountSats} < {
                                        monitoredAddress.AmountSats}. Skipping.");
                                }

                                if (action is not null)
                                {
                                    string txHash = unspentInfo.TransactionHash;
                                    string? transactionData = await this.electrumRpcClient.GetTransactionAsync(txHash, cancellationToken).ConfigureAwait(false);

                                    foreach (OnMonitoredAddressActionCallback callback in callbacks)
                                    {
                                        await callback(action.Value, monitoredAddress, transactionId: unspentInfo.TransactionHash, transactionData: transactionData)
                                            .ConfigureAwait(false);
                                    }

                                    monitoredAddressesToRemove.Add(monitoredAddress);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            this.log.Debug($"Last update of unspent outputs for address '{monitoredAddress.Address}' has been registered before monitoring started at height {
                                monitoredAddress.MonitoringStartedAtHeight}. Skipping.");
                        }
                    }
                }

                lock (this.dataLock)
                {
                    foreach (MonitoredAddress monitoredAddress in monitoredAddressesToRemove)
                    {
                        if (this.monitoredAddresses.Remove(monitoredAddress))
                        {
                            this.log.Debug($"Monitored address '{monitoredAddress}' has been removed from the set after a matching transaction was found.");
                        }
                        else
                        {
                            this.log.Debug($"Monitored address '{
                                monitoredAddress}' should be removed from the set after a matching transaction was found, but it was not found in the set.");
                        }
                    }
                }

                this.log.Debug($"Wait {monitoredAddressTransactionUpdateFrequency} before next round.");
                await Task.Delay(monitoredAddressTransactionUpdateFrequency, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            this.log.Debug("Shutdown detected.");
        }
        catch (Exception e)
        {
            this.log.Error($"Transaction monitor loop failed with exception: {e}");
            throw;
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Processes the new blockchain height. Checks whether any monitored addresses timed out.
    /// </summary>
    /// <param name="blockchainHeight">New blockchain height.</param>
    /// <returns>List of monitored addresses that timed out.</returns>
    /// <remarks>The caller is responsible for holding <see cref="dataLock"/>.</remarks>
    private List<MonitoredAddress> ProcessNewBlockchainHeightLocked(int blockchainHeight)
    {
        this.log.Debug($"* {nameof(blockchainHeight)}={blockchainHeight}");

        List<MonitoredAddress> result = new();
        foreach (MonitoredAddress monitoredAddress in this.monitoredAddresses)
        {
            if (monitoredAddress.TimeoutHeight >= blockchainHeight)
            {
                this.log.Debug($"Monitored address `{monitoredAddress}` has timed out at blockchain height {blockchainHeight}.");
                result.Add(monitoredAddress);
            }
        }

        foreach (MonitoredAddress monitoredAddress in result)
        {
            if (this.monitoredAddresses.Remove(monitoredAddress)) this.log.Debug($"Monitored address '{monitoredAddress}' has expired and has been removed from the set.");
            else this.log.Debug($"Monitored address '{monitoredAddress}' has expired but could not be removed from the set.");
        }

        this.log.Debug($"|$|={result.Count}");
        return result;
    }

    /// <summary>
    /// Registers a callback method to be called when a monitored address action occurs.
    /// </summary>
    /// <param name="callback">Callback method to be called when a monitored address action occurs.</param>
    public void RegisterOnMonitoredAddressActionCallback(OnMonitoredAddressActionCallback callback)
    {
        this.log.Debug("*");

        lock (this.dataLock)
        {
            this.onMonitoredAddressActions.Add(callback);
            this.log.Debug($"Currently {this.onMonitoredAddressActions.Count} addresses are monitored.");
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Registers a new Bitcoin address to be monitored for incoming transactions relevant to the swap with the specified ID.
    /// </summary>
    /// <param name="swapId">ID of the swap that the monitored address is related to.</param>
    /// <param name="address">Bitcoin address to monitor.</param>
    /// <param name="amountSats">Amount expected to be received to this address in satoshis.</param>
    /// <param name="requiredConfirmations">Number of confirmations required.</param>
    /// <param name="timeoutHeight">Blockchain height at which the monitoring should timeout.</param>
    public void RegisterMonitoredAddress(long swapId, string address, long amountSats, int requiredConfirmations, int timeoutHeight)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(address)}='{address}',{nameof(amountSats)}={amountSats},{nameof(requiredConfirmations)}={requiredConfirmations},{
            nameof(timeoutHeight)}={timeoutHeight}");

        lock (this.dataLock)
        {
            MonitoredAddress monitoredAddress = new(swapId: swapId, address, amountSats: amountSats, requiredConfirmations: requiredConfirmations, timeoutHeight: timeoutHeight,
                monitoringStartedAtHeight: this.blockchainHeight);

            if (!this.monitoredAddresses.Add(monitoredAddress))
                throw new SanityCheckException($"Unable to add monitored address '{monitoredAddress}' to the set.");

            this.log.Debug($"Monitored address `{monitoredAddress}` registered at height {this.blockchainHeight}. Currently, {
                this.monitoredAddresses.Count} addresses are monitored.");
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

        this.log.Debug("Waiting for the blockchain height sync task to finish.");
        await this.blockHeightSyncTask.JoinAsync().ConfigureAwait(false);

        this.log.Debug("Waiting for the transaction monitoring loop task to finish.");
        await this.transactionMonitorTask.JoinAsync().ConfigureAwait(false);

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