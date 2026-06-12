using System.Security.Claims;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Api.Middleware;

public sealed class TenantResolverMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantService currentTenant, ITenantAccessService tenantAccessService)
    {
        currentTenant.Clear();

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();
        var userId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : (Guid?)null;
        var userTenantId = Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var parsedTenantId) ? parsedTenantId : (Guid?)null;
        var isAdminContext = roles.Contains(Roles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            || roles.Contains(Roles.AccountingOfficeAdmin, StringComparer.OrdinalIgnoreCase)
            || roles.Contains(Roles.Accountant, StringComparer.OrdinalIgnoreCase);
        var tenantHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (!isAdminContext)
        {
            if (userId is not null && userTenantId.HasValue)
            {
                if (!await tenantAccessService.CanAccessTenantAsync(userId.Value, userTenantId.Value, context.RequestAborted))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(BaseResponseDto<object>.Fail("Invalid tenant access.", ["The user's tenant is inactive or unavailable."]));
                    return;
                }

                currentTenant.SetTenant(userTenantId.Value);
            }

            await next(context);
            return;
        }

        if (!string.IsNullOrWhiteSpace(tenantHeader) && !Guid.TryParse(tenantHeader, out _))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(BaseResponseDto<object>.Fail("Invalid tenant header.", ["X-Tenant-Id must be a valid GUID."]));
            return;
        }

        if (Guid.TryParse(tenantHeader, out var selectedTenantId))
        {
            if (userId is null || !await tenantAccessService.CanAccessTenantAsync(userId.Value, selectedTenantId, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(BaseResponseDto<object>.Fail("Invalid tenant access.", ["The selected tenant is inactive or not assigned to this user."]));
                return;
            }

            currentTenant.SetTenant(selectedTenantId);
        }

        await next(context);
    }
}
