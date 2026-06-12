namespace AccountingSaaS.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    bool IsAccountingOfficeAdmin { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
}

public interface ICurrentTenantService
{
    Guid? TenantId { get; }
    bool IsTenantSelected { get; }
    void SetTenant(Guid tenantId);
    void Clear();
}

public static class TenantDefaults
{
    // TODO: Remove default tenant fallback after authentication and tenant resolution are completed.
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
