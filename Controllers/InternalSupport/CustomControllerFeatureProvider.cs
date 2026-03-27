using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace WhalesExchangeBackend.Controllers.InternalSupport;

/// <summary>
/// Provider that allows to use internal controllers.
/// </summary>
/// <remarks>Based on <see href="https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1134#issuecomment-1668302128"/>.</remarks>
internal class CustomControllerFeatureProvider : ControllerFeatureProvider
{
    /// <inheritdoc/>
    protected override bool IsController(TypeInfo typeInfo)
    {
        bool isCustomController = !typeInfo.IsAbstract && typeof(InternalControllerBase).IsAssignableFrom(typeInfo);
        return isCustomController || base.IsController(typeInfo);
    }
}