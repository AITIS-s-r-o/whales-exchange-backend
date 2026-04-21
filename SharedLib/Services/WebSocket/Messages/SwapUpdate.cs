using System.Globalization;
using System.Text.Json.Serialization;
using WhalesExchangeBackend.SharedLib.Data;
using WhalesExchangeBackend.SharedLib.Models;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesExchangeBackend.SharedLib.Services.WebSocket.Messages;

/// <summary>
/// Description of a swap update.
/// </summary>
internal class SwapUpdate
{
    /// <summary>Frontend swap ID.</summary>
    [JsonPropertyName("id")]
    public string FrontendId { get; }

    /// <summary>New status of the swap.</summary>
    /// <remarks>See <c>SwapStatus*</c> constants in <see cref="Constants"/>.</remarks>
    [JsonPropertyName("status")]
    public string Status { get; }

    /// <summary>Reason of the failure if the swap failed, or <c>null</c> if no failure is reported.</summary>
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; }

    /// <summary>Description of a Bitcoin transaction associated with the swap update, or <c>null</c> if no transaction is associated.</summary>
    [JsonPropertyName("transaction")]
    public SwapStatusTransaction? Transaction { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="frontendId">Frontend swap ID.</param>
    /// <param name="status">New status of the swap.</param>
    /// <param name="failureReason">Reason of the failure if the swap failed, or <c>null</c> if no failure is reported.</param>
    /// <param name="transaction">Description of a Bitcoin transaction associated with the swap update, or <c>null</c> if no transaction is associated.</param>
    [JsonConstructor]
    public SwapUpdate(string frontendId, string status, string? failureReason, SwapStatusTransaction? transaction)
    {
        this.FrontendId = frontendId;
        this.Status = status;
        this.FailureReason = failureReason;
        this.Transaction = transaction;
    }

    /// <summary>
    /// Creates an instance of <see cref="SwapUpdate"/> from <see cref="DbSwap"/>.
    /// </summary>
    /// <param name="swap">Database description of a swap.</param>
    /// <returns><see cref="SwapUpdate"/> instance that describes the swap.</returns>
    public static SwapUpdate FromDbSwap(DbSwap swap)
    {
        string frontendId = swap.FrontendId;

        string status;
        string? failureReason = null;
        SwapStatusTransaction? transaction = null;

        if (swap.IsForward)
        {
            // TODO
            status = Constants.SwapStatusUnknown;
        }
        else
        {
            status = FrontendStatusFromSwapStatus(swap.Status);

            switch (swap.Status)
            {
                case SwapStatus.FundingTxCreated:
                case SwapStatus.FundingTxConfirmed:
                {
                    if (swap.FundingTxId is null)
                        throw new SanityCheckException($"Swap ID {swap.Id} is in {swap.Status} status but its funding TXID is not set.");

                    transaction = new(hex: swap.FundingTxData, id: swap.FundingTxId);
                    break;
                }

                case SwapStatus.ProviderErrorNotAccepted:
                {
                    failureReason = "Client's swap request has not been accepted by the selected swap provider.";
                    break;
                }

                case SwapStatus.ClientErrorFundingTxNotSpent:
                {
                    failureReason = "Client failed to claim the funding transaction output.";
                    break;
                }

                case SwapStatus.ErrorFundingTxNotCreated:
                {
                    failureReason = "Either the lightning invoices were not paid or the swap provider failed to broadcast the funding transaction.";
                    break;
                }
            }
        }

        return new(frontendId: frontendId, status: status, failureReason: failureReason, transaction);
    }

    /// <summary>
    /// Converts <see cref="SwapStatus"/> to frontend swap status constant.
    /// </summary>
    /// <param name="status">Swap status to convert.</param>
    /// <returns>Frontend string constant that corresponds to the swap status.</returns>
    private static string FrontendStatusFromSwapStatus(SwapStatus status)
    {
        return status switch
        {
            SwapStatus.Created => Constants.SwapStatusPendingSwapCreated,
            SwapStatus.Accepted => Constants.SwapStatusPendingSwapCreated,
            SwapStatus.FundingTxCreated => Constants.SwapStatusPendingTransactionMempool,
            SwapStatus.FundingTxConfirmed => Constants.SwapStatusPendingTransactionConfirmed,
            SwapStatus.FundingTxSpent => Constants.SwapStatusSuccessTransactionClaimed,
            SwapStatus.ProviderErrorNotAccepted => Constants.SwapStatusFailedSwapRejected,
            SwapStatus.ClientErrorFundingTxNotSpent => Constants.SwapStatusFailedSwapRefunded,
            SwapStatus.ErrorFundingTxNotCreated => Constants.SwapStatusFailedTransactionLockupFailed,
            _ => Constants.SwapStatusUnknown,
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}=`{5}`,{6}=`{7}`]",
            nameof(this.FrontendId), this.FrontendId,
            nameof(this.Status), this.Status,
            nameof(this.FailureReason), this.FailureReason,
            nameof(this.Transaction), this.Transaction
        );
    }
}