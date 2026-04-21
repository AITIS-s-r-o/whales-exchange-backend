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
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Exceptions;
using WhalesExchangeBackend.SharedLib.Services.WebSocket;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Monitor of blockchain data fetched from the Electrum backend client.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class BlockchainDataMonitor : System.IAsyncDisposable
{
    /// <summary>Frequency with which the information about the Electrum blockchain height is updated.</summary>
    /// <remarks>The value here was selected in order to provide reasonable frequency of updates while distributing queries to Electrum server in time.</remarks>
    private static readonly TimeSpan blockchainHeightUpdateFrequency = TimeSpan.FromSeconds(63);

    /// <summary>Frequency with which the monitored addresses are checked for matching transactions.</summary>
    /// <remarks>
    /// The value here was selected in order to provide reasonable frequency of updates while distributing queries to Electrum server in time.
    /// <para>
    /// Specifically in case of transaction updates, the frequency should be higher in order to deliver the information as soon as possible to the frontend, but not too high to
    /// waste too many resources.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan monitoredAddressTransactionUpdateFrequency = TimeSpan.FromSeconds(13);

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Client that communicates with Electrum RPC server.</summary>
    private readonly ElectrumRpcClient electrumRpcClient;

    /// <summary>Provider of access to swaps in the database.</summary>
    private readonly SwapRepository swapRepository;

    /// <summary>Manager of swap subscriptions.</summary>
    private readonly SubscriptionManager subscriptionManager;

    /// <summary>Cancellation source announcing termination of the instance, i.e. disconnection.</summary>
    private readonly CancellationTokenSource shutdownTokenSource;

    /// <summary>Background task that checks and processes updates to blockchain height.</summary>
    private readonly JoinableTask blockHeightSyncTask;

    /// <summary>Background task that checks and processes matches to monitored addresses.</summary>
    private readonly JoinableTask transactionMonitorTask;

    /// <summary>Set of monitored Bitcoin addresses.</summary>
    /// <remarks>All access has to be protected by <see cref="dataLock"/>.</remarks>
    private readonly HashSet<MonitoredAddress> monitoredAddresses;

    /// <summary>Lock object to be used when accessing <see cref="blockchainHeight"/> and <see cref="monitoredAddresses"/>.</summary>
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
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="electrumRpcClient">Client that communicates with Electrum RPC server.</param>
    /// <param name="swapRepository">Provider of access to swaps in the database.</param>
    /// <param name="subscriptionManager">Manager of swap subscriptions.</param>
    /// <param name="joinableTaskFactory">Factory for starting async tasks running in the background.</param>
    public BlockchainDataMonitor(ElectrumRpcClient electrumRpcClient, SwapRepository swapRepository, SubscriptionManager subscriptionManager,
        JoinableTaskFactory joinableTaskFactory)
    {
        this.log.Debug("*");

        this.dataLock = new();
        this.disposedValueLock = new();
        this.shutdownTokenSource = new();

        this.electrumRpcClient = electrumRpcClient;
        this.swapRepository = swapRepository;
        this.subscriptionManager = subscriptionManager;

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

                            IReadOnlyList<MonitoredAddress> expiredMonitoredAddresses = Array.Empty<MonitoredAddress>();
                            lock (this.dataLock)
                            {
                                if (this.blockchainHeight != response.BlockchainHeight)
                                {
                                    this.blockchainHeight = response.BlockchainHeight;
                                    expiredMonitoredAddresses = this.ProcessNewBlockchainHeightLocked(this.blockchainHeight);
                                }
                            }

                            this.log.Debug($"{expiredMonitoredAddresses.Count} monitored addresses timed out.");

                            List<MonitoredAddress> monitoredAddressesToRemove = new();
                            foreach (MonitoredAddress monitoredAddress in expiredMonitoredAddresses)
                            {
                                bool stopMonitoring = await this.OnMonitoredAddressActionAsync(MonitoredAddressAction.Timeout, monitoredAddress, transactionId: null,
                                    outputIndex: null, transactionData: null, cancellationToken).ConfigureAwait(false);

                                if (stopMonitoring)
                                    monitoredAddressesToRemove.Add(monitoredAddress);
                            }

                            this.StopMonitoringAddresses(monitoredAddressesToRemove);
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
                        // Electrum returns unspent outputs sorted by block height in ascending order, so the last one is the newest. However, if the transaction is still in its
                        // mempool, the block height is returned as 0. We can ignore all records with block height prior to monitoring start.
                        bool isLastUtxoInMempool = response[^1].BlockHeight == 0;
                        if (isLastUtxoInMempool || (response[^1].BlockHeight > monitoredAddress.MonitoringStartedAtHeight))
                        {
                            // Iterate in reverse to process records with higher block heights first.
                            for (int i = response.Count - 1; i >= 0; i--)
                            {
                                AddressUnspentInfo unspentInfo = response[i];

                                if (!unspentInfo.InMempool && (unspentInfo.BlockHeight < monitoredAddress.MonitoringStartedAtHeight))
                                    break;

                                MonitoredAddressAction? action = null;
                                if (unspentInfo.AmountSats >= monitoredAddress.AmountSats)
                                {
                                    if (unspentInfo.InMempool)
                                    {
                                        if (!monitoredAddress.MempoolActionReported)
                                        {
                                            this.log.Debug($"Monitored address '{monitoredAddress.Address}' received a new unspent output with amount {
                                                unspentInfo.AmountSats} satoshis and it has no confirmation yet.");

                                            action = MonitoredAddressAction.InMempool;
                                            monitoredAddress.MempoolActionReported = true;
                                        }
                                        else this.log.Debug($"Transaction in mempool has already been reported for monitored address '{monitoredAddress}'.");
                                    }
                                    else
                                    {
                                        int confirmations = currentBlockHeight - unspentInfo.BlockHeight + 1;
                                        this.log.Debug($"Monitored address '{monitoredAddress.Address}' received a new unspent output with amount {
                                            unspentInfo.AmountSats} satoshis at blockchain height {unspentInfo.BlockHeight}. Current height is {
                                            currentBlockHeight}, so the output has {confirmations}/{monitoredAddress.RequiredConfirmations} confirmations.");

                                        if (confirmations >= monitoredAddress.RequiredConfirmations)
                                            action = MonitoredAddressAction.Confirmed;
                                    }
                                }
                                else
                                {
                                    this.log.Debug($"Value of unspent output '{unspentInfo.TransactionHash}:{unspentInfo.OutputIndex}' is {unspentInfo.AmountSats} < {
                                        monitoredAddress.AmountSats}. Ignoring and continuing monitoring.");
                                }

                                if (action is not null)
                                {
                                    this.log.Debug($"Action set to {action} for monitored address '{monitoredAddress}'.");

                                    string txHash = unspentInfo.TransactionHash;
                                    string? transactionData = null;

                                    try
                                    {
                                        transactionData = await this.electrumRpcClient.GetTransactionAsync(txHash, cancellationToken).ConfigureAwait(false);
                                    }
                                    catch (ElectrumRpcException e)
                                    {
                                        this.log.Error($"Electrum server reported error when querying transaction ID '{txHash}': {e}");
                                    }

                                    bool success = await this.OnMonitoredAddressActionAsync(action.Value, monitoredAddress, transactionId: unspentInfo.TransactionHash,
                                        unspentInfo.OutputIndex, transactionData: transactionData, cancellationToken).ConfigureAwait(false);

                                    if (success)
                                    {
                                        // If the monitoring address is a lockup address, we need to wait for confirmation, so we only stop monitoring after the transaction is
                                        // confirmed. In case of claim/refund transactions, we can stop monitoring as soon as we see the transaction in mempool, because the
                                        // timelock is what protect us there and the secret is gone at that point as well, so there is nothing we can do about it later anyway.
                                        MonitoredAddressAction act = action.Value;
                                        bool stopMonitoring = (monitoredAddress.IsLockupAddress && (act == MonitoredAddressAction.Confirmed))
                                            || (!monitoredAddress.IsLockupAddress && ((act == MonitoredAddressAction.InMempool) || (act == MonitoredAddressAction.Confirmed)));

                                        if (stopMonitoring)
                                            monitoredAddressesToRemove.Add(monitoredAddress);
                                    }
                                    else this.log.Warn($"Monitoring of address '{monitoredAddress.Address}' will continue as processing the action {action} failed.");

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

                this.StopMonitoringAddresses(monitoredAddressesToRemove);

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
            if (monitoredAddress.TimeoutHeight <= blockchainHeight)
            {
                this.log.Debug($"Monitored address `{monitoredAddress}` has timed out at blockchain height {blockchainHeight}.");
                result.Add(monitoredAddress);
            }
        }

        foreach (MonitoredAddress monitoredAddress in result)
        {
            if (!this.monitoredAddresses.Remove(monitoredAddress))
                throw new SanityCheckException($"Monitored address '{monitoredAddress}' has expired but could not be removed from the set.");

            this.log.Debug($"Monitored address '{monitoredAddress}' has expired and has been removed from the set.");
        }

        this.log.Debug($"|$|={result.Count}");
        return result;
    }

    /// <summary>
    /// Callback method to be called when a monitored address action occurs.
    /// </summary>
    /// <param name="action">Action that occurred on the monitored address.</param>
    /// <param name="monitoredAddress">Monitored address that triggered the action.</param>
    /// <param name="transactionId">Bitcoin transaction ID in hex format, or <c>null</c> if <paramref name="action"/> is <see cref="MonitoredAddressAction.Timeout"/>.</param>
    /// <param name="outputIndex">Index of the output in the Bitcoin transaction, or <c>null</c> if <paramref name="action"/> is <see cref="MonitoredAddressAction.Timeout"/>.
    /// </param>
    /// <param name="transactionData">Raw transaction data in hex format, or <c>null</c> if <paramref name="action"/> is <see cref="MonitoredAddressAction.Timeout"/>.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns><c>true</c> if the function succeeded, <c>false</c> otherwise. If <c>true</c> is returned, the address monitoring may stop.</returns>
    private async Task<bool> OnMonitoredAddressActionAsync(MonitoredAddressAction action, MonitoredAddress monitoredAddress, string? transactionId, int? outputIndex,
        string? transactionData, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(action)}={action},{nameof(monitoredAddress)}='{monitoredAddress}',{nameof(transactionId)}='{transactionId}',{nameof(outputIndex)}={outputIndex},{
            nameof(transactionData)}='{transactionData.ToBoundedString()}'");

        bool result = false;

        if (monitoredAddress.IsLockupAddress)
        {
            DbSwap? swap = null;
            try
            {
                switch (action)
                {
                    case MonitoredAddressAction.InMempool:
                    case MonitoredAddressAction.Confirmed:
                        if (transactionId is null)
                            throw new SanityCheckException($"Transaction ID is required for action {action}.");

                        if (outputIndex is null)
                            throw new SanityCheckException($"Output index is required for action {action}.");

                        bool isConfirmed = action == MonitoredAddressAction.Confirmed;
                        swap = await this.swapRepository.FundingTransactionSetAsync(monitoredAddress.SwapId, isConfirmed, transactionId: transactionId,
                            outputIndex: outputIndex.Value, transactionData: transactionData).ConfigureAwait(false);
                        break;

                    case MonitoredAddressAction.Timeout:
                        swap = await this.swapRepository.FundingOrClaimTransactionTimeoutAsync(monitoredAddress.SwapId, isFundingTransaction: true).ConfigureAwait(false);
                        break;

                    default:
                        throw new SanityCheckException($"Invalid action provided {action}.");
                }
            }
            catch (DatabaseException e)
            {
                this.log.Error($"Exception occurred while trying to update database record of swap ID {monitoredAddress.SwapId}: {e}");
            }

            if (swap is not null)
            {
                if (action == MonitoredAddressAction.Confirmed)
                {
                    if (swap.TimeoutBlockHeight is null)
                        throw new SanityCheckException($"Timeout block height is null for swap ID {swap.Id}.");

                    // Before we propagate the update to the frontend, which will cause the frontend to claim the funding transaction output, we need to start monitoring
                    // the client address.
                    this.RegisterMonitoredAddress(swapId: swap.Id, address: swap.ClientAddress, amountSats: swap.AmountToReceiveSats, requiredConfirmations: 1,
                        timeoutHeight: swap.TimeoutBlockHeight.Value, isLockupAddress: false);
                }

                SwapUpdate swapUpdate = SwapUpdate.FromDbSwap(swap);
                await this.subscriptionManager.PropagateSwapUpdateAsync(swapUpdate, cancellationToken).ConfigureAwait(false);
                result = true;
            }
        }
        else
        {
            DbSwap? swap = null;
            switch (action)
            {
                case MonitoredAddressAction.InMempool:
                case MonitoredAddressAction.Confirmed:
                    if (transactionId is null)
                        throw new SanityCheckException($"Transaction ID is required for action {action}.");

                    swap = await this.HandleClientAddressTransactionAsync(monitoredAddress, transactionId: transactionId, transactionData: transactionData, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case MonitoredAddressAction.Timeout:
                    try
                    {
                        swap = await this.swapRepository.FundingOrClaimTransactionTimeoutAsync(monitoredAddress.SwapId, isFundingTransaction: false).ConfigureAwait(false);
                    }
                    catch (DatabaseException e)
                    {
                        this.log.Error($"Exception occurred while trying to update database record of swap ID {monitoredAddress.SwapId}: {e}");
                    }

                    break;

                default:
                    throw new SanityCheckException($"Invalid action provided {action}.");
            }

            if (swap is not null)
            {
                SwapUpdate swapUpdate = SwapUpdate.FromDbSwap(swap);
                await this.subscriptionManager.PropagateSwapUpdateAsync(swapUpdate, cancellationToken).ConfigureAwait(false);
                result = true;
            }
        }

        this.log.Debug($"$={result}");
        return result;
    }

    /// <summary>
    /// Handles the confirmation of a transaction that spends to a client address. Anyone can send funds to a monitored client address, so we need to check whether the transaction
    /// is relevant to the swap, which is when the input of the transaction references the swap's lockup output.
    /// </summary>
    /// <param name="monitoredAddress">Monitored address that triggered the action.</param>
    /// <param name="transactionId">Bitcoin transaction ID in hex format, or <c>null</c>.</param>
    /// <param name="transactionData">Bitcoin transaction data in hex format, or <c>null</c> if not available.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>The updated swap, or <c>null</c> if no relevant swap was found.</returns>
    private async Task<DbSwap?> HandleClientAddressTransactionAsync(MonitoredAddress monitoredAddress, string transactionId, string? transactionData,
        CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(monitoredAddress)}='{monitoredAddress}',{nameof(transactionId)}='{transactionId}',{
            nameof(transactionData)}='{transactionData.ToBoundedString()}'");

        if (transactionData is null)
        {
            try
            {
                transactionData = await this.electrumRpcClient.GetTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false);
            }
            catch (ElectrumRpcException e)
            {
                this.log.Error($"Electrum server reported error when querying transaction ID '{transactionId}': {e}");
            }
        }

        DbSwap? result = null;
        if (transactionData is not null)
        {
            DbSwap? swap = null;
            try
            {
                swap = await this.swapRepository.GetSwapByIdAsync(monitoredAddress.SwapId).ConfigureAwait(false);
            }
            catch (DatabaseException e)
            {
                this.log.Error($"Exception occurred while trying to get swap ID {monitoredAddress.SwapId} from the database: {e}");
            }

            if (swap is not null)
            {
                if ((swap.FundingTxId is not null) && (swap.LockupOutputIndex is not null))
                {
                    ElectrumTransaction? transaction = null;
                    try
                    {
                        transaction = await this.electrumRpcClient.DeserializeAsync(transactionData, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        this.log.Error($"Electrum server reported error when deserializing transaction data for transaction ID '{transactionId}': {e}");
                    }

                    if (transaction is not null)
                    {
                        bool spendsLockupOutput = false;
                        foreach (ElectrumTransactionInput input in transaction.Inputs)
                        {
                            if ((input.PrevoutHash == swap.FundingTxId) && (input.PrevoutIndex == swap.LockupOutputIndex.Value))
                            {
                                this.log.Debug($"Transaction '{transactionId}' spends the lockup output {swap.FundingTxId}:{swap.LockupOutputIndex.Value} of swap {swap.Id}.");
                                spendsLockupOutput = true;
                                break;
                            }
                        }

                        if (spendsLockupOutput)
                        {
                            this.log.Debug($"Swap {swap.Id} was claimed.");
                            result = await this.swapRepository.ReverseSwapClaimedAsync(monitoredAddress.SwapId, transactionId, transactionData).ConfigureAwait(false);
                        }
                    }
                }
                else this.log.Debug($"Swap ID {swap.Id} has no funding transaction information.");
            }
        }
        else this.log.Debug($"Transaction ID '{transactionId}' cannot be found.");

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// Registers a new Bitcoin address to be monitored for incoming transactions relevant to the swap with the specified ID.
    /// </summary>
    /// <param name="swapId">ID of the swap that the monitored address is related to.</param>
    /// <param name="address">Bitcoin address to monitor.</param>
    /// <param name="amountSats">Amount expected to be received to this address in satoshis.</param>
    /// <param name="requiredConfirmations">Number of confirmations required.</param>
    /// <param name="timeoutHeight">Blockchain height at which the monitoring should timeout.</param>
    /// <param name="isLockupAddress"><c>true</c> if the monitored address is the lockup address in the funding transaction, <c>false</c> if it is the destination address.</param>
    public void RegisterMonitoredAddress(long swapId, string address, long amountSats, int requiredConfirmations, long timeoutHeight, bool isLockupAddress)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(address)}='{address}',{nameof(amountSats)}={amountSats},{nameof(requiredConfirmations)}={requiredConfirmations},{
            nameof(timeoutHeight)}={timeoutHeight},{nameof(isLockupAddress)}={isLockupAddress}");

        lock (this.dataLock)
        {
            MonitoredAddress monitoredAddress = new(swapId: swapId, address, amountSats: amountSats, requiredConfirmations: requiredConfirmations, timeoutHeight: timeoutHeight,
                monitoringStartedAtHeight: this.blockchainHeight, isLockupAddress);

            if (!this.monitoredAddresses.Add(monitoredAddress))
                throw new SanityCheckException($"Unable to add monitored address '{monitoredAddress}' to the set.");

            this.log.Debug($"Monitored address `{monitoredAddress}` registered at height {this.blockchainHeight}. Currently, {
                this.monitoredAddresses.Count} addresses are monitored.");
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Stops monitoring the given Bitcoin addresses.
    /// </summary>
    /// <param name="monitoredAddressesToRemove">Monitored Bitcoin addresses to stop monitoring.</param>
    private void StopMonitoringAddresses(List<MonitoredAddress> monitoredAddressesToRemove)
    {
        this.log.Debug($"* |{nameof(monitoredAddressesToRemove)}|={monitoredAddressesToRemove.Count}");

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