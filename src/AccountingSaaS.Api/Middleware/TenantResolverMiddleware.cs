using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Api.Middleware;

public sealed class TenantResolverMiddleware
{
    private readonly RequestDelegate next;

    public TenantResolverMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentSessionService currentSession,
        ICurrentTenantService currentTenant,
        AppDbContext dbContext)
    {
        currentTenant.Clear();
        dbContext.DisableTenantFilter = false;

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (context.Request.Headers.ContainsKey("X-Tenant-Id") ||
            context.Request.Query.ContainsKey("tenantId") ||
            context.Request.Query.ContainsKey("companyId"))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                BaseResponseDto<object>.Fail(
                    "TenantId and CompanyId must come from JWT claims only."));
            return;
        }

        try
        {
            var userId = currentSession.UserId;
            var tenantId = currentSession.TenantId;
            _ = currentSession.ActiveFinancialYearId;
            _ = currentSession.UserName;
            _ = currentSession.Email;
            _ = currentSession.CompanyId;
            _ = currentSession.CompanyCode;
            _ = currentSession.CompanyNameAr;
            _ = currentSession.CompanyNameEn;
            _ = currentSession.IsSuperAdmin;
            _ = currentSession.Language;

            var userIsActive = await dbContext.Users
                .IgnoreQueryFilters()
                .AnyAsync(
                    user =>
                        user.Id == userId &&
                        user.TenantId == tenantId &&
                        user.IsActive &&
                        !user.IsDeleted,
                    context.RequestAborted);
            if (!userIsActive)
            {
                await WriteUnauthorizedAsync(
                    context,
                    "The user session is no longer active.");
                return;
            }

            var tenantIsActive = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(
                    tenant =>
                        tenant.Id == tenantId &&
                        tenant.IsActive &&
                        !tenant.IsDeleted,
                    context.RequestAborted);
            if (!tenantIsActive)
            {
                await WriteForbiddenAsync(
                    context,
                    "The company is inactive or unavailable.");
                return;
            }

            await next(context);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteUnauthorizedAsync(context, exception.Message);
        }
    }

    private static async Task WriteUnauthorizedAsync(
        HttpContext context,
        string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            BaseResponseDto<object>.Fail(message));
    }

    private static async Task WriteForbiddenAsync(
        HttpContext context,
        string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            BaseResponseDto<object>.Fail(message));
    }
}
