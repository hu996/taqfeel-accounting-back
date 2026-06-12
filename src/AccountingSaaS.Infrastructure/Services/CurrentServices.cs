using System.Security.Claims;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Email => User?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public bool IsSuperAdmin => Roles.Contains(AccountingSaaS.Domain.Constants.Roles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
    public bool IsAccountingOfficeAdmin => Roles.Contains(AccountingSaaS.Domain.Constants.Roles.AccountingOfficeAdmin, StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct().ToList() ?? [];
    public IReadOnlyList<string> Permissions => User?.FindAll("permission").Select(x => x.Value).Distinct().ToList() ?? [];
}

public sealed class CurrentTenantService : ICurrentTenantService
{
    private Guid? _tenantId;

    // TODO: Remove default tenant fallback after authentication and tenant resolution are completed.
    public Guid? TenantId => _tenantId is { } tenantId && tenantId != Guid.Empty
        ? tenantId
        : TenantDefaults.DefaultTenantId;

    public bool IsTenantSelected => TenantId.HasValue;
    public void SetTenant(Guid tenantId) => _tenantId = tenantId == Guid.Empty ? null : tenantId;
    public void Clear() => _tenantId = null;
}
