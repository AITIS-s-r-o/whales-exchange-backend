using System.Globalization;
using System.Threading;

namespace WhalesExchangeBackend.Services.DataProvider;

/// <summary>
/// Information about monitored Bitcoin address.
/// </summary>
internal class MonitoredAddress
{
    /// <summary>Lock object to be used when accessing the field of <see cref="MempoolActionReported"/>.</summary>
    private readonly Lock statusLock;

    /// <summary>ID of the swap that the monitored address is related to.</summary>
    public long SwapId { get; }

    /// <summary>Bitcoin address to monitor.</summary>
    public string Address { get; }

    /// <summary>Amount expected to be received to this address in satoshis.</summary>
    /// <remarks>The amount must arrive in a single output with value at least equal to the amount, otherwise the transaction is ignored.</remarks>
    public long AmountSats { get; }

    /// <summary>Number of confirmations required.</summary>
    public int RequiredConfirmations { get; }

    /// <summary>Blockchain height at which the monitoring should timeout.</summary>
    public long TimeoutHeight { get; }

    /// <summary>Blockchain height at which the monitoring started.</summary>
    public int MonitoringStartedAtHeight { get; }

    /// <summary><c>true</c> if the monitored address is the lockup address in the funding transaction, <c>false</c> if it is the client address.</summary>
    public bool IsLockupAddress { get; }

    /// <summary><c>true</c> if the mempool action was already reported, <c>false</c> otherwise.</summary>
    public bool MempoolActionReported
    {
        get
        {
            lock (this.statusLock)
            {
                return field;
            }
        }

        set
        {
            lock (this.statusLock)
            {
                field = value;
            }
        }
    }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="swapId">ID of the swap that the monitored address is related to.</param>
    /// <param name="address">Bitcoin address to monitor.</param>
    /// <param name="requiredConfirmations">Number of confirmations required.</param>
    /// <param name="amountSats">Amount expected to be received to this address in satoshis.</param>
    /// <param name="timeoutHeight">Blockchain height at which the monitoring should timeout.</param>
    /// <param name="monitoringStartedAtHeight">Blockchain height at which the monitoring started.</param>
    /// <param name="isLockupAddress"><c>true</c> if the monitored address is the lockup address in the funding transaction, <c>false</c> if it is the client address.</param>
    public MonitoredAddress(long swapId, string address, long amountSats, int requiredConfirmations, long timeoutHeight, int monitoringStartedAtHeight, bool isLockupAddress)
    {
        this.statusLock = new();
        this.SwapId = swapId;
        this.Address = address;
        this.AmountSats = amountSats;
        this.RequiredConfirmations = requiredConfirmations;
        this.TimeoutHeight = timeoutHeight;
        this.MonitoringStartedAtHeight = monitoringStartedAtHeight;
        this.IsLockupAddress = isLockupAddress;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`,{4}={5},{6}={7},{8}={9},{10}={11},{12}={13},{14}={15}]",
            nameof(this.SwapId), this.SwapId,
            nameof(this.Address), this.Address,
            nameof(this.AmountSats), this.AmountSats,
            nameof(this.RequiredConfirmations), this.RequiredConfirmations,
            nameof(this.TimeoutHeight), this.TimeoutHeight,
            nameof(this.MonitoringStartedAtHeight), this.MonitoringStartedAtHeight,
            nameof(this.IsLockupAddress), this.IsLockupAddress,
            nameof(this.MempoolActionReported), this.MempoolActionReported
        );
    }
}