using Microsoft.Extensions.DependencyInjection;

namespace WhalesExchangeBackend.Controllers.InternalSupport;

/// <summary>
/// Extension methods for enabling internal controllers in the application.
/// </summary>
/// <remarks>Based on <see href="https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1134#issuecomment-1668302128"/>.</remarks>
internal static class InternalControllersExtension
{
    /// <summary>
    /// Enables the use of internal controllers in the application.
    /// </summary>
    /// <param name="builder">Service builder object.</param>
    /// <returns>MVC builder object to allow using fluent syntax.</returns>
    public static IMvcBuilder EnableInternalControllers(this IMvcBuilder builder)
    {
        _ = builder.ConfigureApplicationPartManager(manager =>
        {
            manager.FeatureProviders.Add(new CustomControllerFeatureProvider());
        });

        return builder;
    }
}