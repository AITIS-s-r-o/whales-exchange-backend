using System.Net.WebSockets;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket;

/// <summary>
/// Constants of the WebSocket protocol.
/// </summary>
internal static class Constants
{
    /// <summary>Maximum size of a single message in bytes.</summary>
    public const int MaxMessageSizeBytes = 1024 * 1024;

    /// <summary>Type of WebSocket messages used in client-server communication.</summary>
    public const WebSocketMessageType MessageType = WebSocketMessageType.Text;

    /// <summary>Name of the channel for swap updates.</summary>
    public const string SwapUpdatesChannel = "swap.update";

    /// <summary>Name of the subscribe operation.</summary>
    public const string OperationSubscribe = "subscribe";

    /// <summary>Name of the unsubscribe operation.</summary>
    public const string OperationUnsubscribe = "unsubscribe";

    /// <summary>Name of the pending swap status when the LN invoice is set.</summary>
    public const string SwapStatusPendingInvoiceSet = "invoice.set";

    /// <summary>Name of the pending swap status when the LN invoice has been paid.</summary>
    public const string SwapStatusPendingInvoicePaid = "invoice.paid";

    /// <summary>Name of the pending swap status when the LN invoice is pending.</summary>
    public const string SwapStatusPendingInvoicePending = "invoice.pending";

    /// <summary>Name of the pending swap status when the swap was created.</summary>
    public const string SwapStatusPendingSwapCreated = "swap.created";

    /// <summary>Name of the pending swap status when the client confirmed the swap.</summary>
    public const string SwapStatusPendingTransactionConfirmed = "transaction.confirmed";

    /// <summary>Name of the pending swap status when the Bitcoin transaction is seen in a mempool by the client.</summary>
    public const string SwapStatusPendingTransactionMempool = "transaction.mempool";

    /// <summary>Name of the pending swap status when the zero-confirmation Bitcoin transaction has been rejected.</summary>
    public const string SwapStatusPendingTransactionZeroConfRejected = "transaction.zeroconf.rejected";

    /// <summary>Name of the pending swap status when the claim Bitcoin transaction is pending.</summary>
    public const string SwapStatusPendingTransactionClaimPending = "transaction.claim.pending";

    /// <summary>Name of the pending swap status when the Bitcoin transaction is seen in a mempool by the server.</summary>
    public const string SwapStatusPendingTransactionServerMempool = "transaction.server.mempool";

    /// <summary>Name of the pending swap status when the server confirmed the swap.</summary>
    public const string SwapStatusPendingTransactionServerConfirmed = "transaction.server.confirmed";

    /// <summary>Name of the failed swap status when the swap was rejected by the swap provider.</summary>
    /// <remarks>Note that this status is not available in the original Boltz implementation.</remarks>
    public const string SwapStatusFailedSwapRejected = "swap.rejected";

    /// <summary>Name of the failed swap status when the swap expired.</summary>
    public const string SwapStatusFailedSwapExpired = "swap.expired";

    /// <summary>Name of the failed swap status when the refund has been issued because the client never spent the funding Bitcoin transaction.</summary>
    public const string SwapStatusFailedSwapRefunded = "swap.refunded";

    /// <summary>Name of the failed swap status when the refund is expected.</summary>
    public const string SwapStatusFailedSwapWaitingForRefund = "swap.waitingForRefund";

    /// <summary>Name of the failed swap status when the LN invoice has expired..</summary>
    public const string SwapStatusFailedInvoiceExpired = "invoice.expired";

    /// <summary>Name of the failed swap status when the LN invoice has not been paid.</summary>
    public const string SwapStatusFailedInvoiceFailedToPay = "invoice.failedToPay";

    /// <summary>Name of the failed swap status when the Bitcoin lockup failed.</summary>
    public const string SwapStatusFailedTransactionFailed = "transaction.failed";

    /// <summary>Name of the failed swap status when the Bitcoin lockup transaction failed to be broadcasted by the server.</summary>
    public const string SwapStatusFailedTransactionLockupFailed = "transaction.lockupFailed";

    /// <summary>Name of the failed swap status when the Bitcoin transaction was refunded.</summary>
    public const string SwapStatusFailedTransactionRefunded = "transaction.refunded";

    /// <summary>Name of the successful swap status when the client's LN invoice was settled..</summary>
    public const string SwapStatusSuccessInvoiceSettled = "invoice.settled";

    /// <summary>Name of the successful swap status when the reverse swap Bitcoin transaction was claimed.</summary>
    public const string SwapStatusSuccessTransactionClaimed = "transaction.claimed";

    /// <summary>Name of the swap status that is unknown.</summary>
    /// <remarks>Note that this status is not available in the original Boltz implementation.</remarks>
    public const string SwapStatusUnknown= "swap.unknown";
}