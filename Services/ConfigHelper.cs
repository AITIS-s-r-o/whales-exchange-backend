using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend.Services;

/// <summary>
/// Server configuration helper.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by ASP.NET Core DI as a singleton.")]
internal class ConfigHelper
{
    /// <summary>Key identifier of the application domain setting.</summary>
    private const string SettingsDomainKey = "Domain";

    /// <summary>Key identifier of the database connection string.</summary>
    private const string SettingsConnectionStringKey = "ConnectionString";

    /// <summary>Key identifier of the Electrum RPC configuration.</summary>
    private const string SettingsElectrumRpcKey = "ElectrumRpc";

    /// <summary>Key identifier of the Electrum RPC URI string.</summary>
    private const string SettingsElectrumRpcUriKey = "Uri";

    /// <summary>Key identifier of the Electrum RPC user name string.</summary>
    private const string SettingsElectrumRpcUserKey = "User";

    /// <summary>Key identifier of the Electrum RPC password string.</summary>
    private const string SettingsElectrumRpcPassKey = "Pass";

    /// <summary>Instance logger.</summary>
    private readonly WsLogger log = WsLogger.GetCurrentClassLogger();

    /// <summary>Full path to the server's root folder.</summary>
    public string ServerRoot { get; }

    /// <summary>Full path to the server's resource folder.</summary>
    public string ResourcesPath { get; }

    /// <summary>Web domain of the application.</summary>
    public string Domain { get; }

    /// <summary>Database connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Electrum RPC configuration.</summary>
    public ElectrumRpcConfig ElectrumRpcConfig { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="env">Provides information about the web hosting environment an application is running in.</param>
    /// <param name="configuration">Set of key/value application configuration properties.</param>
    public ConfigHelper(IWebHostEnvironment env, IConfiguration configuration)
    {
        this.log.Debug("*");

        this.ServerRoot = env.ContentRootPath;
        this.log.Debug($"Server root path set to '{this.ServerRoot}'.");

        this.ResourcesPath = Path.Combine(this.ServerRoot, "Resources");
        this.log.Debug($"Resource path set to '{this.ResourcesPath}'.");

        // Domain.
        {
            string domain = this.GetRequiredString(configuration, SettingsDomainKey);
            this.Domain = domain;
            this.log.Debug($"Domain set to '{this.Domain}'.");
        }

        // Connection string.
        {
            string connectionString = this.GetRequiredString(configuration, SettingsConnectionStringKey);
            this.ConnectionString = connectionString;
            this.log.Debug($"Connection string set to '{this.ConnectionString}'.");
        }

        // Electrum RPC configuration.
        {
            IConfigurationSection smtpConfigurationSection = configuration.GetRequiredSection(SettingsElectrumRpcKey);
            this.ElectrumRpcConfig = this.LoadElectrumRpcConfiguration(smtpConfigurationSection);
        }

        this.log.Debug("$");
    }

    /// <summary>
    /// Loads Electrum RPC configuration section from the application settings file.
    /// </summary>
    /// <param name="electrumRpcConigurationSection">Section of the application settings file that contains Electrum RPC configuration.</param>
    /// <returns>SMTP configuration.</returns>
    private ElectrumRpcConfig LoadElectrumRpcConfiguration(IConfigurationSection electrumRpcConigurationSection)
    {
        this.log.Debug("*");

        string uriStr = this.GetRequiredString(electrumRpcConigurationSection, SettingsElectrumRpcUriKey);
        string username = this.GetRequiredString(electrumRpcConigurationSection, SettingsElectrumRpcUserKey);
        string password = this.GetRequiredString(electrumRpcConigurationSection, SettingsElectrumRpcPassKey);

        Uri uri = new(uriStr);
        ElectrumRpcConfig electrumRpcConfig = new(uri, user: username, pass: password);

        this.log.Debug($"$='{electrumRpcConfig}'");
        return electrumRpcConfig;
    }

    /// <summary>
    /// Gets a required string value from the application configuration.
    /// </summary>
    /// <param name="configuration">Configuration to retrieve the key from.</param>
    /// <param name="key">Key to retrieve from the configuration.</param>
    /// <returns>Value associated to the key.</returns>
    private string GetRequiredString(IConfiguration configuration, string key)
    {
        string? result = configuration[key];
        if (result is null)
            throw new SanityCheckException($"'{key}' is not set in the application settings file.");

        return result;
    }
}