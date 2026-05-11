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
using WhalesExchangeBackend.SharedLib.Models;
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

    /// <summary>Service that checks client swap limits.</summary>
    private readonly SwapLimitChecker swapLimitChecker;

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
    /// <param name="swapLimitChecker">Service that checks client swap limits.</param>
    /// <param name="swapRepository">Provider of access to swaps in the database.</param>
    /// <param name="subscriptionManager">Manager of swap subscriptions.</param>
    /// <param name="joinableTaskFactory">Factory for starting async tasks running in the background.</param>
    public BlockchainDataMonitor(ElectrumRpcClient electrumRpcClient, SwapLimitChecker swapLimitChecker, SwapRepository swapRepository, SubscriptionManager subscriptionManager,
        JoinableTaskFactory joinableTaskFactory)
    {
        this.log.Debug("*");

        this.dataLock = new();
        this.disposedValueLock = new();
        this.shutdownTokenSource = new();

        this.electrumRpcClient = electrumRpcClient;
        this.swapLimitChecker = swapLimitChecker;
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
                    MonitoredAddressActionInfo? actionInfo;
                    if (monitoredAddress.MonitorSpending)
                    {
                        actionInfo = await this.CheckAddressHistoryAsync(currentBlockHeight, monitoredAddress, spending: true, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        actionInfo = await this.CheckAddressUnspentAsync(currentBlockHeight, monitoredAddress, cancellationToken).ConfigureAwait(false);
                        if (actionInfo is null)
                        {
                            // If there is no unspent output for the monitored address, it may be because no transaction has been created yet, or it may be because a transaction
                            // spending to the monitored address has been created but the output has been spent already. In the latter case, we need to check the full address
                            // history not to miss the action.
                            actionInfo = await this.CheckAddressHistoryAsync(currentBlockHeight, monitoredAddress, spending: false, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (actionInfo is not null)
                    {
                        string txHash = actionInfo.TransactionHash;

                        bool success = await this.OnMonitoredAddressActionAsync(actionInfo.Action, monitoredAddress, transactionId: txHash, actionInfo.OutputIndex,
                            transactionData: actionInfo.TransactionData, cancellationToken).ConfigureAwait(false);

                        if (success)
                        {
                            // If the monitoring address is a lockup address, we need to wait for confirmation, so we only stop monitoring after the transaction is
                            // confirmed. In case of claim/refund transactions, we can stop monitoring as soon as we see the transaction in mempool, because the
                            // timelock is what protect us there and the secret is gone at that point as well, so there is nothing we can do about it later anyway.
                            MonitoredAddressAction act = actionInfo.Action;
                            bool stopMonitoring = (monitoredAddress.IsLockupAddress && (act == MonitoredAddressAction.Confirmed))
                                || (!monitoredAddress.IsLockupAddress && ((act == MonitoredAddressAction.InMempool) || (act == MonitoredAddressAction.Confirmed)));

                            if (stopMonitoring)
                                monitoredAddressesToRemove.Add(monitoredAddress);
                        }
                        else this.log.Warn($"Monitoring of address '{monitoredAddress.Address}' will continue as processing the action {actionInfo.Action} failed.");
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
    /// Checks whether there is an unspent output for the monitored address that matches the monitoring criteria.
    /// </summary>
    /// <param name="currentBlockHeight">Current block height.</param>
    /// <param name="monitoredAddress">Monitored address which expects a payment.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>Information about monitoring action, or <c>null</c> if no matching output is found.</returns>
    private async Task<MonitoredAddressActionInfo?> CheckAddressUnspentAsync(long currentBlockHeight, MonitoredAddress monitoredAddress, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(currentBlockHeight)}={currentBlockHeight},{nameof(monitoredAddress)}='{monitoredAddress}'");

        MonitoredAddressActionInfo? result = null;
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
            bool isLastUtxoInMempool = response[^1].InMempool;
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
                            // If the transaction is confirmed already but we have not refreshed our blockchain height since then, we can take the UTXO block height
                            // instead of the current blockchain height.
                            if (unspentInfo.BlockHeight > currentBlockHeight)
                                currentBlockHeight = unspentInfo.BlockHeight;

                            int confirmations = (int)(currentBlockHeight - unspentInfo.BlockHeight + 1);
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
                            monitoredAddress.AmountSats}. Ignoring and continuing monitoring.");
                    }

                    if (action is not null)
                    {
                        this.log.Debug($"Action set to {action} for monitored address '{monitoredAddress}'.");

                        string? transactionData = null;

                        try
                        {
                            transactionData = await this.electrumRpcClient.GetTransactionAsync(unspentInfo.TransactionHash, cancellationToken).ConfigureAwait(false);
                        }
                        catch (ElectrumRpcException e)
                        {
                            this.log.Error($"Electrum server reported error when querying transaction ID '{unspentInfo.TransactionHash}': {e}");
                        }

                        if (transactionData is null)
                        {
                            this.log.Debug($"Transaction data is not available for transaction ID '{unspentInfo.TransactionHash}'.");
                            continue;
                        }

                        result = new(action.Value, transactionHash: unspentInfo.TransactionHash, unspentInfo.OutputIndex, transactionData: transactionData);
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

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// Checks the history of a monitored address to see if a new transaction has been received that pays to it or spends from it.
    /// </summary>
    /// <param name="currentBlockHeight">Current block height.</param>
    /// <param name="monitoredAddress">Monitored address.</param>
    /// <param name="spending"><c>true</c> to check for spending transactions, <c>false</c> to check for receiving transactions.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>Information about monitoring action, or <c>null</c> if no matching output is found.</returns>
    private async Task<MonitoredAddressActionInfo?> CheckAddressHistoryAsync(long currentBlockHeight, MonitoredAddress monitoredAddress, bool spending,
        CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(currentBlockHeight)}={currentBlockHeight},{nameof(monitoredAddress)}='{monitoredAddress}',{nameof(spending)}={spending}");

        MonitoredAddressActionInfo? result = null;
        ElectrumGetAddressHistoryResponse? response = null;
        try
        {
            response = await this.electrumRpcClient.GetAddressHistoryAsync(monitoredAddress.Address, cancellationToken).ConfigureAwait(false);
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
            // Electrum returns history entries in chronological order. If we process the list in the reverse order, once we reach an entry with block height prior to monitoring
            // start, we can stop processing the history. However, if the transaction is still in its mempool, the block height is returned as 0. We can ignore all records with
            // block height prior to monitoring start.
            //
            // First, we find the index of the oldest transaction that is relevant.
            int firstIndex = -1;
            bool isLastTransactionInMempool = response[^1].InMempool;
            if (isLastTransactionInMempool || (response[^1].BlockHeight > monitoredAddress.MonitoringStartedAtHeight))
            {
                for (int i = response.Count - 1; i >= 0; i--)
                {
                    AddressHistoryInfo historyInfo = response[i];
                    if (!historyInfo.InMempool && (historyInfo.BlockHeight < monitoredAddress.MonitoringStartedAtHeight))
                        break;

                    firstIndex = i;
                }
            }

            if (firstIndex != -1)
            {
                // In the list of relevant transactions, we are looking for an output that matches the monitored criteria. We process transactions from the first relevant to
                // the last.
                for (int i = firstIndex; i < response.Count; i++)
                {
                    AddressHistoryInfo historyInfo = response[i];

                    string? transactionData = null;

                    try
                    {
                        transactionData = await this.electrumRpcClient.GetTransactionAsync(historyInfo.TransactionHash, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ElectrumRpcException e)
                    {
                        this.log.Error($"Electrum server reported error when querying transaction ID '{historyInfo.TransactionHash}': {e}");
                    }

                    if (transactionData is null)
                    {
                        this.log.Debug($"Transaction data is not available for transaction ID '{historyInfo.TransactionHash}'.");
                        continue;
                    }

                    ElectrumTransaction? transaction = null;
                    try
                    {
                        transaction = await this.electrumRpcClient.DeserializeAsync(transactionData, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        this.log.Error($"Electrum server reported error when deserializing transaction data for transaction ID '{historyInfo.TransactionHash}': {e}");
                    }

                    if (transaction is null)
                    {
                        this.log.Debug($"Transaction ID '{historyInfo.TransactionHash}' cannot be deserialized.");
                        continue;
                    }

                    int outputIndex = 0;
                    ElectrumTransactionOutput? relevantOutput = null;
                    if (spending)
                    {
                        if (monitoredAddress.FundingTransactionHash is null)
                            throw new SanityCheckException($"Funding transaction hash is null for monitored address '{monitoredAddress}'.");

                        if (monitoredAddress.FundingOutputIndex is null)
                            throw new SanityCheckException($"Funding output index is null for monitored address '{monitoredAddress}'.");

                        for (int inputIndex = 0; inputIndex < transaction.Inputs.Count; inputIndex++)
                        {
                            ElectrumTransactionInput input = transaction.Inputs[inputIndex];
                            if ((input.PrevoutHash == monitoredAddress.FundingTransactionHash) && (input.PrevoutIndex == monitoredAddress.FundingOutputIndex))
                            {
                                this.log.Debug($"Found input '{input.PrevoutHash}:{input.PrevoutIndex}' spending from the monitored address '{
                                    monitoredAddress.Address}' amount {monitoredAddress.AmountSats} satoshis.");
                                relevantOutput = new(address: monitoredAddress.Address, valueSats: monitoredAddress.AmountSats, scriptPubKey: string.Empty);
                                outputIndex = input.PrevoutIndex;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (; outputIndex < transaction.Outputs.Count; outputIndex++)
                        {
                            ElectrumTransactionOutput output = transaction.Outputs[outputIndex];
                            if (output.Address == monitoredAddress.Address)
                            {
                                if (output.ValueSats >= monitoredAddress.AmountSats)
                                {
                                    this.log.Debug($"Found output creation for monitored address '{monitoredAddress.Address}' with amount {output.ValueSats} satoshis in '{
                                        historyInfo.TransactionHash}:{outputIndex}'.");
                                    relevantOutput = output;
                                    break;
                                }

                                this.log.Debug($"Value of output '{historyInfo.TransactionHash}:{outputIndex}' is {output.ValueSats} < {
                                    monitoredAddress.AmountSats}. Ignoring and continuing monitoring.");
                            }
                        }
                    }

                    if (relevantOutput is null)
                        continue;

                    MonitoredAddressAction? action = this.GetAddressHistoryAction(historyInfo, currentBlockHeight: currentBlockHeight, monitoredAddress,
                        amount: relevantOutput.ValueSats);

                    if (action is not null)
                    {
                        this.log.Debug($"Action set to {action} for monitored address '{monitoredAddress}'.");

                        result = new(action.Value, transactionHash: historyInfo.TransactionHash, outputIndex, transactionData: transactionData);
                        break;
                    }
                }
            }
        }

        this.log.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// For the given address history entry, checks whether the transaction matches the monitoring criteria.
    /// </summary>
    /// <param name="historyInfo">Monitored address history entry.</param>
    /// <param name="currentBlockHeight">Current blockchain height.</param>
    /// <param name="monitoredAddress">The monitored address.</param>
    /// <param name="amount">Amount associated with the transaction.</param>
    /// <returns>Detected action on the monitored address, or <c>null</c> if no action occurred.</returns>
    private MonitoredAddressAction? GetAddressHistoryAction(AddressHistoryInfo historyInfo, long currentBlockHeight, MonitoredAddress monitoredAddress, long amount)
    {
        this.log.Debug($"* {nameof(historyInfo)}='{historyInfo}',{nameof(currentBlockHeight)}={currentBlockHeight},{nameof(monitoredAddress)}='{monitoredAddress}',{
            nameof(amount)}={amount}");

        MonitoredAddressAction? result = null;
        if (historyInfo.InMempool)
        {
            if (!monitoredAddress.MempoolActionReported)
            {
                if (monitoredAddress.MonitorSpending)
                {
                    this.log.Debug($"Amount {amount} satoshis has been spent from the monitored address '{monitoredAddress.Address}' and it has no confirmation yet.");
                }
                else
                {
                    this.log.Debug($"Monitored address '{monitoredAddress.Address}' received an output with amount {amount} satoshis and it has no confirmation yet.");
                }

                result = MonitoredAddressAction.InMempool;
                monitoredAddress.MempoolActionReported = true;
            }
            else this.log.Debug($"Transaction in mempool has already been reported for monitored address '{monitoredAddress}'.");
        }
        else
        {
            // If the transaction is confirmed already but we have not refreshed our blockchain height since then, we can take the transaction block height instead of the current
            // blockchain height.
            if (historyInfo.BlockHeight > currentBlockHeight)
                currentBlockHeight = historyInfo.BlockHeight;

            int confirmations = (int)(currentBlockHeight - historyInfo.BlockHeight + 1);
            if (monitoredAddress.MonitorSpending)
            {
                this.log.Debug($"Amount {amount} satoshis has been spent from the monitored address '{monitoredAddress.Address}' at blockchain height {
                    historyInfo.BlockHeight}. Current height is {currentBlockHeight}, so the new output has {confirmations}/{
                    monitoredAddress.RequiredConfirmations} confirmations.");
            }
            else
            {
                this.log.Debug($"Monitored address '{monitoredAddress.Address}' received an unspent output with amount {amount} satoshis at blockchain height {
                    historyInfo.BlockHeight}. Current height is {currentBlockHeight}, so the output has {confirmations}/{monitoredAddress.RequiredConfirmations} confirmations.");
            }

            if (confirmations >= monitoredAddress.RequiredConfirmations)
                result = MonitoredAddressAction.Confirmed;
        }

        this.log.Debug($"$={result}");
        return result;
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
        DbSwap? swap = null;

        if (monitoredAddress.IsLockupAddress)
        {
            try
            {
                switch (action)
                {
                    case MonitoredAddressAction.InMempool:
                    case MonitoredAddressAction.Confirmed:
                        if (transactionId is null)
                            throw new SanityCheckException($"Transaction ID is required for action {action}.");

                        if (transactionData is null)
                            throw new SanityCheckException($"Transaction data is required for action {action}.");

                        if (outputIndex is null)
                            throw new SanityCheckException($"Output index is required for action {action}.");

                        bool isConfirmed = action == MonitoredAddressAction.Confirmed;
                        this.log.Debug($"Funding transaction of swap ID {monitoredAddress.SwapId} was {
                            (action == MonitoredAddressAction.InMempool ? "broadcasted" : "confirmed")}.");

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

                    if (transactionId is null)
                        throw new SanityCheckException($"Transaction ID is required for action {action}.");

                    if (outputIndex is null)
                        throw new SanityCheckException($"Output index is required for action {action}.");

                    // For forward swap, the only way for us to detect the end of forward swap is to monitor whether the funding transaction locked output has been spent.
                    // Before the timeout expiration, only swap provider can spend. In that case, we mark the swap as completed successfully.
                    //
                    // For reverse swap, before we also want to start monitoring spending of the funding transaction locked output.
                    this.RegisterMonitoredAddress(swapId: swap.Id, frontendId: swap.FrontendId, address: monitoredAddress.Address, amountSats: swap.AmountToReceiveSats,
                        requiredConfirmations: 1, timeoutHeight: swap.TimeoutBlockHeight.Value, isLockupAddress: false, monitorSpending: true,
                        fundingTransactionHash: transactionId, fundingOutputIndex: outputIndex);
                }
            }
        }
        else
        {
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
        }

        if (swap is not null)
        {
            if (swap.Status > SwapStatus.Accepted)
                _ = this.swapLimitChecker.UnregisterSwap(swap.FrontendId);

            SwapUpdate swapUpdate = SwapUpdate.FromDbSwap(swap);
            await this.subscriptionManager.PropagateSwapUpdateAsync(swapUpdate, cancellationToken).ConfigureAwait(false);
            result = true;
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
    /// <returns>Updated swap, or <c>null</c> if no relevant swap was found.</returns>
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
                                this.log.Debug($"Transaction '{transactionId}' spends the lockup output '{swap.FundingTxId}:{swap.LockupOutputIndex.Value}' of swap {swap.Id}.");
                                spendsLockupOutput = true;
                                break;
                            }
                        }

                        if (spendsLockupOutput)
                        {
                            this.log.Debug($"Swap ID {swap.Id} was claimed.");

                            try
                            {
                                result = await this.swapRepository.SwapClaimedAsync(monitoredAddress.SwapId, transactionId: transactionId, transactionData: transactionData)
                                    .ConfigureAwait(false);
                            }
                            catch (DatabaseException e)
                            {
                                this.log.Error($"Exception occurred while marking swap ID {monitoredAddress.SwapId} as claimed: {e}");

                                // Nothing else to do here, the swap will be marked as claimed in the next monitoring round when we detect the same transaction again.
                            }
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
    /// <param name="frontendId">Frontend ID of the swap that the monitored address is related to.</param>
    /// <param name="address">Bitcoin address to monitor.</param>
    /// <param name="amountSats">Amount expected to be received to this address in satoshis.</param>
    /// <param name="requiredConfirmations">Number of confirmations required.</param>
    /// <param name="timeoutHeight">Blockchain height at which the monitoring should timeout.</param>
    /// <param name="isLockupAddress"><c>true</c> if the monitored address is the lockup address in the funding transaction, <c>false</c> if it is the destination address.</param>
    /// <param name="monitorSpending"><c>true</c> to monitor sending from the address, <c>false</c> to monitor sending money to the address.</param>
    /// <param name="fundingTransactionHash">If <paramref name="monitorSpending"/> is true, this contains the hash of the funding transaction; <c>null</c> otherwise.</param>
    /// <param name="fundingOutputIndex">If <paramref name="monitorSpending"/> is true, this contains the index of the funding output; <c>null</c> otherwise.</param>
    public void RegisterMonitoredAddress(long swapId, string frontendId, string address, long amountSats, int requiredConfirmations, long timeoutHeight, bool isLockupAddress,
        bool monitorSpending, string? fundingTransactionHash, int? fundingOutputIndex)
    {
        this.log.Debug($"* {nameof(swapId)}={swapId},{nameof(frontendId)}='{frontendId}',{nameof(address)}='{address}',{nameof(amountSats)}={amountSats},{
            nameof(requiredConfirmations)}={requiredConfirmations},{nameof(timeoutHeight)}={timeoutHeight},{nameof(isLockupAddress)}={isLockupAddress},{
            nameof(monitorSpending)}={monitorSpending},{nameof(fundingTransactionHash)}='{fundingTransactionHash}',{nameof(fundingOutputIndex)}={fundingOutputIndex}");

        lock (this.dataLock)
        {
            MonitoredAddress monitoredAddress = new(swapId: swapId, frontendId: frontendId, address: address, amountSats: amountSats, requiredConfirmations: requiredConfirmations,
                timeoutHeight: timeoutHeight, monitoringStartedAtHeight: this.blockchainHeight, isLockupAddress: isLockupAddress, monitorSpending: monitorSpending,
                fundingTransactionHash: fundingTransactionHash, fundingOutputIndex: fundingOutputIndex);

            if (!this.monitoredAddresses.Add(monitoredAddress))
                throw new SanityCheckException($"Unable to add monitored address '{monitoredAddress}' to the set.");

            this.log.Debug($"Monitored address `{monitoredAddress}` registered at height {this.blockchainHeight}. Currently, {
                this.monitoredAddresses.Count} addresses are monitored.");
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Unregisters a Bitcoin address of a swap from monitoring.
    /// </summary>
    /// <param name="frontendId">Frontend ID of the swap that the monitored address is related to.</param>
    public void UnregisterMonitoredAddressWithFrontendId(string frontendId)
    {
        this.log.Debug($"* {nameof(frontendId)}='{frontendId}'");

        lock (this.dataLock)
        {
            MonitoredAddress? swapMonitoredAddress = null;
            foreach (MonitoredAddress monitoredAddress in this.monitoredAddresses)
            {
                if (monitoredAddress.FrontendId == frontendId)
                {
                    swapMonitoredAddress = monitoredAddress;
                    break;
                }
            }

            if (swapMonitoredAddress is not null)
            {
                if (this.monitoredAddresses.Remove(swapMonitoredAddress))
                {
                    this.log.Debug($"Monitored address '{swapMonitoredAddress}' has been removed from the set after a matching transaction was found.");
                }
                else
                {
                    this.log.Debug($"Monitored address '{
                        swapMonitoredAddress}' should be removed from the set after a matching transaction was found, but it was not found in the set.");
                }
            }
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