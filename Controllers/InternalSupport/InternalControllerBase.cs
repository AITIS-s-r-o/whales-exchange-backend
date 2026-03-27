using Microsoft.AspNetCore.Mvc;

namespace WhalesExchangeBackend.Controllers.InternalSupport;

/// <summary>
/// Base class for internal controllers.
/// </summary>
/// <remarks>Based on <see href="https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1134#issuecomment-1668302128"/>.</remarks>
internal class InternalControllerBase : Controller
{
}