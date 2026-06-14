using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AccountingSaaS.Api.Filters;

public sealed class ModuleAccessFilter : IAsyncActionFilter
{
    private static readonly IReadOnlyDictionary<string, string> ControllerModules =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FixedAssets"] = "FixedAssets",
            ["BankReconciliation"] = "BankReconciliation",
            ["RecurringEntries"] = "RecurringEntries",
            ["ReportBuilder"] = "ReportBuilder"
        };

    private readonly ICurrentSessionService currentSession;

    public ModuleAccessFilter(ICurrentSessionService currentSession)
    {
        this.currentSession = currentSession;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor &&
            ControllerModules.TryGetValue(
                descriptor.ControllerName,
                out var requiredModule) &&
            !currentSession.IsSuperAdmin &&
            !currentSession.HasModule(requiredModule))
        {
            context.Result = new ObjectResult(
                BaseResponseDto<object>.Fail(
                    "Module is not enabled for this company."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
