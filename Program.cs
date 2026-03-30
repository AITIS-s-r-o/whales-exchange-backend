using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.Threading;
using WhalesExchangeBackend.Controllers.InternalSupport;
using WhalesExchangeBackend.Data;
using WhalesExchangeBackend.Data.Repository;
using WhalesExchangeBackend.Services;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesExchangeBackend;

/// <summary>
/// Main application class that contains program entry point.
/// </summary>
public static class Program
{
    /// <summary>Class logger.</summary>
    private static readonly WsLogger clog = WsLogger.GetCurrentClassLogger();

    /// <summary>Application shutdown cancellation token, or <c>null</c> if not initialized yet.</summary>
    private static CancellationToken? shutdownToken;

    /// <summary>
    /// Program's entry point.
    /// </summary>
    /// <param name="args">Arguments of the program.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        clog.Debug($"* {nameof(args)}={args.LogJoin()}");

        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            _ = builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                string[] supportedCultures = new[] { "en-US" };

                _ = options.SetDefaultCulture(supportedCultures[0])
                    .AddSupportedCultures(supportedCultures)
                    .AddSupportedUICultures(supportedCultures);
            });

            _ = builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAnyOriginPolicy", policy =>
                {
                    _ = policy.AllowAnyOrigin();
                    _ = policy.AllowAnyMethod();
                    _ = policy.AllowAnyHeader();
                });
            });

            // Add services to the container.
            _ = builder.Services.AddControllersWithViews()
                .EnableInternalControllers()
                .AddCookieTempDataProvider(options =>
                {
                    options.Cookie.IsEssential = true;
                });

            _ = builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            _ = builder.Services.AddHttpContextAccessor();

            _ = builder.Services.AddSingleton<ConfigHelper>();
            _ = builder.Services.AddSingleton<JoinableTaskContext>();
            _ = builder.Services.AddSingleton<JoinableTaskFactory>();
            _ = builder.Services.AddSingleton<DbLocks>();
            _ = builder.Services.AddSingleton((IServiceProvider serviceProvider) =>
            {
#if DEBUG
#pragma warning disable CA5359 // Do Not Disable Certificate Validation
                SslClientAuthenticationOptions options = new()
                {
                    RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                };
#pragma warning restore CA5359 // Do Not Disable Certificate Validation
#else
                SslClientAuthenticationOptions? options = null;
#endif

                // The class is disposed by the HTTP client below as the ownership of the object is moved.
                SocketsHttpHandler socketHandler = new()
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    SslOptions = options,
                };

                return new HttpClient(socketHandler);
            });

            // Users.
            _ = builder.Services.AddDbContext<ApplicationDbContext>((IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder) =>
            {
                ConfigHelper configHelper = serviceProvider.GetRequiredService<ConfigHelper>();
                _ = optionsBuilder.UseSqlite(configHelper.ConnectionString);
            });

            // DB context factory is a singleton that creates per-request database contexts.
            _ = builder.Services.AddSingleton<ApplicationDbContextFactory>();

            // Database repositories provide access to individual tables of the database and are created on per-request basis.
            _ = builder.Services.AddSingleton<SwapProviderRepository>();

            // Electrum RPC connectivity and related services.
            _ = builder.Services.AddSingleton<ElectrumRpcClient>();
            _ = builder.Services.AddSingleton<SwapProviderFetcher>();

            // These configuration files are used when "dotnet ef" command is executed.
            _ = builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

            // This configuration file contain sensitive information.
            _ = builder.Configuration.AddJsonFile("config.secret.json", optional: false, reloadOnChange: false);

            WebApplication app = builder.Build();

            _ = app.UseRequestLocalization();

            IHostApplicationLifetime appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            shutdownToken = appLifetime.ApplicationStopping;

            if (app.Environment.IsDevelopment())
            {
                // Shows detailed error pages in development.
                _ = app.UseDeveloperExceptionPage();
                _ = builder.Configuration.AddJsonFile("appsettings.Debug.json", optional: false, reloadOnChange: false);

                // This configuration file contain sensitive information.
                _ = builder.Configuration.AddJsonFile("config.secret.Debug.json", optional: false, reloadOnChange: false);
            }
            else
            {
                // Global error handler for production.
                _ = app.UseExceptionHandler("/server-error");
                _ = app.UseHsts();
                _ = builder.Configuration.AddJsonFile("appsettings.Release.json", optional: false, reloadOnChange: false);

                // This configuration files contain sensitive information.
                _ = builder.Configuration.AddJsonFile("config.secret.Release.json", optional: false, reloadOnChange: false);
            }

            // Handle 404 and other non-exception errors.
            _ = app.UseStatusCodePagesWithReExecute("/error/{0}");
            _ = app.UseHttpsRedirection();
            _ = app.UseRouting();

            // Note: CORS middleware must be placed between UseRouting and UseAuthorization/UseAuthentication.
            // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-9.0#middleware-order
            _ = app.UseCors();

            _ = app.UseAuthorization();
            _ = app.UseAuthentication();
            _ = app.MapControllerRoute
            (
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}"
            );

            // WebSockets for signals service.
            _ = app.UseWebSockets();

            _ = app.Map("/ws", async (HttpContext context) =>
            {
                if (shutdownToken is null)
                    throw new SanityCheckException("Shutdown token has not been initialized yet.");

                using IServiceScope scope = context.RequestServices.CreateScope();
                /* TODO */
            });

            // Trigger instantiation of the swap provider fetcher.
            _ = app.Services.GetRequiredService<SwapProviderFetcher>();

            // Initialize and seed database at startup.
            using (IServiceScope scope = app.Services.CreateScope())
            {
                ApplicationDbContextFactory contextFactory = scope.ServiceProvider.GetRequiredService<ApplicationDbContextFactory>();
                using (ApplicationDbContext context = contextFactory.CreateDbContext())
                {
                    if (context.Database.GetPendingMigrations().Any())
                        context.Database.Migrate();
                }
            }

            await app.RunAsync().ConfigureAwait(false);
            clog.Debug("$");
        }
        catch (Exception e)
        {
            clog.Error($"Exception occurred while running the web application: {e}");
            clog.Debug("$<EXCEPTION>");
            throw;
        }
        finally
        {
            clog.FlushAndShutDown();
        }
    }
}