namespace WhalesExchangeBackend.SharedLib.Models;

/// <summary>
/// Status of the swap.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Success states have values less than <c>100</c>.</item>
/// <item>Error state values are between <c>100</c> and <c>199</c> for provider failures.</item>
/// <item>Error state values are between <c>200</c> and <c>299</c> for client failures.</item>
/// <item>Error state values are between <c>300</c> and <c>399</c> for faults that cannot be assigned to either side due to lack of information.</item>
/// </list>
/// Note that when the hundreds parts is removed (i.e. code modulo <c>100</c>), the status values are ordered chronologically.
/// </remarks>
internal enum SwapStatus
{
    /// <summary>Swap request was received from the client and sent to the provider.</summary>
    Created = 0,

    /// <summary>Swap request was accepted by the provider and generated fee and HODL invoices.</summary>
    Accepted = 2,

    /// <summary>Client paid both invoices and provider broadcasted the funding transaction.</summary>
    FundingTxCreated = 4,

    /// <summary>Funding transaction has enough confirmations for the client to proceed.</summary>
    FundingTxConfirmed = 5,

    /// <summary>Client spent the funding transaction output.</summary>
    FundingTxSpent = 7,

    /// <summary>Error occurred before the request was accepted. This includes unreachable provider, or provider actively rejecting the swap.</summary>
    ProviderErrorNotAccepted = 101,

    /// <summary>Client failed to spend the funding transaction before expiration.</summary>
    ClientErrorFundingTxNotSpent = 206,

    /// <summary>Either the user did not pay both invoices, or the provider failed to create the funding transaction before expiration.</summary>
    ErrorFundingTxNotCreated = 303,

    /// <summary>Status of the swap cannot be determined.</summary>
    Unknown = 999,
}