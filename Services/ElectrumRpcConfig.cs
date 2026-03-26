using System;
using System.Globalization;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Application configuration for Electrum RPC connection.
/// </summary>
internal class ElectrumRpcConfig
{
    /// <summary>URI of the Electrum RPC endpoint.</summary>
    public Uri Uri { get; }

    /// <summary>Electrum RPC user name.</summary>
    public string User { get; }

    /// <summary>Electrum RPC password.</summary>
    public string Pass { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="uri">URI of the Electrum RPC endpoint.</param>
    /// <param name="user">Electrum RPC user name.</param>
    /// <param name="pass">Electrum RPC password.</param>
    public ElectrumRpcConfig(Uri uri, string user, string pass)
    {
        this.Uri = uri;
        this.User = user;
        this.Pass = pass;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}=`{3}`]",
            nameof(this.Uri), this.Uri,
            nameof(this.User), this.User
        );
    }
}