using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <summary>
/// Manager of swap subscriptions.
/// <para>The main role of the manager is to deliver updates for swaps to the connected clients.</para>
/// </summary>
internal class SubscriptionManager
{
    /// <summary>Maximum number of swap IDs per message.</summary>
    private const int MaxIdsPerMessage = 200;

    /// <summary>Maximum number of swap IDs that a single client can be subscribed to.</summary>
    private const int MaxIdsPerClient = 1000;

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Provider of access to swaps in the database.</summary>
    private readonly ISwapRepository swapRepository;

    /// <summary>Frontend swap IDs mapped to the set of client connection handlers who are subscribed to them.</summary>
    /// <remarks>
    /// Note that the client connection handlers are not owned by the manager.
    /// <para>All access to this object must be protected by <see cref="clientLock"/>.</para>
    /// </remarks>
    private readonly Dictionary<string, HashSet<ClientConnectionHandler>> swapIdsToClients;

    /// <summary>Client connection handlers mapped to the set of frontend swap IDs they are subscribed to.</summary>
    /// <remarks>All access to this object must be protected by <see cref="clientLock"/>.</remarks>
    private readonly Dictionary<ClientConnectionHandler, HashSet<string>> clientsToSwapIds;

    /// <summary>Lock object to protect access to <see cref="swapIdsToClients"/>, and <see cref="clientsToSwapIds"/>.</summary>
    private readonly Lock clientLock;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="swapRepository">Provider of access to swaps in the database.</param>
    public SubscriptionManager(ISwapRepository swapRepository)
    {
        this.log.Debug("*");

        this.swapRepository = swapRepository;

        this.clientLock = new();
        this.swapIdsToClients = new();
        this.clientsToSwapIds = new();

        this.log.Debug("$");
    }

    /// <summary>
    /// Subscribes the client to the specified frontend swap IDs.
    /// </summary>
    /// <param name="frontendSwapIds">List of frontend swap IDs to subscribe to.</param>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns><c>true</c> if the subscription was successful, <c>false</c> otherwise.</returns>
    public async Task<bool> SubscribeAsync(string[] frontendSwapIds, ClientConnectionHandler clientConnectionHandler, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(frontendSwapIds)}={frontendSwapIds.LogJoin()},{nameof(clientConnectionHandler)}='{clientConnectionHandler}'");

        if ((frontendSwapIds is null) || (frontendSwapIds.Length > MaxIdsPerMessage))
        {
            if (frontendSwapIds is null) this.log.Debug("$<NULL_IDS>=false");
            else this.log.Debug($"$<TOO_MANY_IDS>=false");

            return false;
        }

        // Frontend swap ID comes from the client and thus can be null.
        frontendSwapIds = frontendSwapIds.Select(c => c ?? string.Empty).ToArray();

        lock (this.clientLock)
        {
            if (!this.clientsToSwapIds.TryGetValue(clientConnectionHandler, out HashSet<string>? swapIds))
            {
                swapIds = new();
                this.clientsToSwapIds.Add(clientConnectionHandler, swapIds);
            }

            if (swapIds.Count + frontendSwapIds.Length > MaxIdsPerClient)
            {
                this.log.Debug($"{frontendSwapIds.Length} new swap IDs will not be subscribed for client ID '{
                    clientConnectionHandler}' as this client would reach over the maximum number of subscription.");
                this.log.Debug("$<CLIENT_MAX_SUBSCRIPTIONS>=false");
                return false;
            }

            foreach (string frontendSwapId in frontendSwapIds)
            {
                if (string.IsNullOrEmpty(frontendSwapId))
                    continue;

                if (!this.swapIdsToClients.TryGetValue(frontendSwapId, out HashSet<ClientConnectionHandler>? clients))
                {
                    clients = new();
                    this.swapIdsToClients.Add(frontendSwapId, clients);
                }

                if (clients.Add(clientConnectionHandler))
                {
                    this.log.Debug($"Client ID '{clientConnectionHandler}' has been added to the set of subscribers for frontend swap ID '{frontendSwapId}'.");
                }
                else this.log.Warn($"Client ID '{clientConnectionHandler}' has already been on the set of subscribers for frontend swap ID '{frontendSwapId}'.");

                if (swapIds.Add(frontendSwapId))
                {
                    this.log.Debug($"Frontend swap ID '{frontendSwapId}' has been added to the set of subscribed frontend swap IDs for client ID '{clientConnectionHandler}'.");
                }
                else this.log.Warn($"Frontend swap ID '{frontendSwapId}' has already been on the set of subscribed frontend swap IDs for client ID '{clientConnectionHandler}'.");
            }
        }

        DbSwap?[] swaps = await this.swapRepository.GetSwapsByFrontendIdsAsync(frontendSwapIds).ConfigureAwait(false);

        List<SwapUpdate> swapUpdates = new(capacity: swaps.Length);
        foreach (DbSwap? swap in swaps)
        {
            if (swap is null)
                continue;

            SwapUpdate swapUpdate = SwapUpdate.FromDbSwap(swap);
            swapUpdates.Add(swapUpdate);
        }

        bool success = await clientConnectionHandler.SendSwapUpdateAsync(swapUpdates.ToArray(), cancellationToken).ConfigureAwait(false);
        if (!success)
            this.log.Warn($"Sending swap updates for {frontendSwapIds.Length} frontend swap IDs to client ID '{clientConnectionHandler}' failed.");

        this.log.Debug($"$={success}");
        return success;
    }

    /// <summary>
    /// Unsubscribes the client from the specified frontend swap IDs.
    /// </summary>
    /// <param name="frontendSwapIds">List of frontend swap IDs to unsubscribe from.</param>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint.</param>
    public void Unsubscribe(string[] frontendSwapIds, ClientConnectionHandler clientConnectionHandler)
    {
        this.log.Debug($"* {nameof(frontendSwapIds)}={frontendSwapIds.LogJoin()},{nameof(clientConnectionHandler)}='{clientConnectionHandler}'");

        if ((frontendSwapIds is null) || (frontendSwapIds.Length > MaxIdsPerMessage))
        {
            if (frontendSwapIds is null) this.log.Debug("$<NULL_IDS>=false");
            else this.log.Debug($"$<TOO_MANY_IDS>=false");

            return;
        }

        // Frontend swap ID comes from the client and thus can be null.
        frontendSwapIds = frontendSwapIds.Select(c => c ?? string.Empty).ToArray();

        lock (this.clientLock)
        {
            foreach (string frontendSwapId in frontendSwapIds)
            {
                if (this.swapIdsToClients.TryGetValue(frontendSwapId, out HashSet<ClientConnectionHandler>? clients))
                {
                    if (clients.Remove(clientConnectionHandler))
                    {
                        this.log.Debug($"Client ID '{clientConnectionHandler}' has been removed from the set of subscribers for frontend swap ID '{frontendSwapId}'.");
                    }
                    else this.log.Warn($"Client ID '{clientConnectionHandler}' was not found in the set of subscribers for frontend swap ID '{frontendSwapId}'.");
                }
                else this.log.Debug($"Frontend swap ID '{frontendSwapId}' was not found when trying to remove client ID '{clientConnectionHandler}' from its set of subscribers.");

                if (this.clientsToSwapIds.TryGetValue(clientConnectionHandler, out HashSet<string>? swapIds))
                {
                    if (swapIds.Remove(frontendSwapId))
                    {
                        this.log.Debug($"Frontend swap ID '{frontendSwapId}' has been removed from the set of subscribed frontend swap IDs for client ID '{
                            clientConnectionHandler}'.");
                    }
                    else
                    {
                        this.log.Warn($"Frontend swap ID '{frontendSwapId}' was not found in the set of subscribed frontend swap IDs for client ID '{clientConnectionHandler}'.");
                    }
                }
                else
                {
                    this.log.Debug($"Client ID '{clientConnectionHandler}' was not found when trying to remove frontend swap ID '{
                        frontendSwapId}' from its set of subscribed frontend swap IDs.");
                }
            }
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Removes the given client from the map.
    /// </summary>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint to remove.</param>
    public void RemoveClient(ClientConnectionHandler clientConnectionHandler)
    {
        this.log.Debug($"* {nameof(clientConnectionHandler)}='{clientConnectionHandler}'");

        lock (this.clientLock)
        {
            this.RemoveClientLocked(clientConnectionHandler);
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Removes the given client from the map.
    /// </summary>
    /// <param name="clientConnectionHandler">Handler of client connections to the WebSocket endpoint to remove.</param>
    /// <remarks>The caller is responsible for holding <see cref="clientLock"/>.</remarks>
    private void RemoveClientLocked(ClientConnectionHandler clientConnectionHandler)
    {
        this.log.Debug($"* {nameof(clientConnectionHandler)}='{clientConnectionHandler}'");

        if (this.clientsToSwapIds.Remove(clientConnectionHandler, out HashSet<string>? swapIds))
        {
            this.log.Debug($"Client ID '{clientConnectionHandler}' has been removed from the map.");

            foreach (string swapId in swapIds)
            {
                if (this.swapIdsToClients.TryGetValue(swapId, out HashSet<ClientConnectionHandler>? clients))
                {
                    if (clients.Remove(clientConnectionHandler))
                    {
                        this.log.Debug($"Client ID '{clientConnectionHandler}' has been removed from the set of subscribers for frontend swap ID '{swapId}'.");
                    }
                    else this.log.Warn($"Client ID '{clientConnectionHandler}' was not found on the set of subscribers for frontend swap ID '{swapId}'.");

                    if (clients.Count == 0)
                    {
                        if (this.swapIdsToClients.Remove(swapId))
                        {
                            this.log.Debug($"Frontend swap ID '{swapId}' has been removed from the map because it has no more subscribers.");
                        }
                        else this.log.Warn($"Frontend swap ID '{swapId}' was not found in the map when trying to remove it after its set of subscribers became empty.");
                    }
                }
            }
        }
        else this.log.Debug($"Client ID '{clientConnectionHandler}' has already been removed from the map.");

        this.log.Debug("$");
    }

    /// <summary>
    /// Propagates swap update to the subscribed clients.
    /// </summary>
    /// <param name="swapUpdate">Swap update to propagate.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task PropagateSwapUpdateAsync(SwapUpdate swapUpdate, CancellationToken cancellationToken)
    {
        this.log.Debug($"* {nameof(swapUpdate)}='{swapUpdate}'");

        ClientConnectionHandler[] clientConnectionHandlers = Array.Empty<ClientConnectionHandler>();
        lock (this.clientLock)
        {
            if (this.swapIdsToClients.TryGetValue(swapUpdate.FrontendId, out HashSet<ClientConnectionHandler>? handlers))
                clientConnectionHandlers = handlers.ToArray();
        }

        if (clientConnectionHandlers.Length > 0)
        {
            this.log.Debug($"Swap update '{swapUpdate}' will be propagated to {clientConnectionHandlers.Length} clients.");
            SwapUpdate[] swapUpdates = new SwapUpdate[] { swapUpdate };

            foreach (ClientConnectionHandler clientConnectionHandler in clientConnectionHandlers)
            {
                bool success = await clientConnectionHandler.SendSwapUpdateAsync(swapUpdates, cancellationToken).ConfigureAwait(false);
                if (!success)
                    this.log.Warn($"Sending swap update '{swapUpdate}' to client ID '{clientConnectionHandler}' failed.");
            }
        }
        else this.log.Debug($"No clients are currently subscribed to swap frontend ID '{swapUpdate.FrontendId}'.");

        this.log.Debug("$");
    }
}