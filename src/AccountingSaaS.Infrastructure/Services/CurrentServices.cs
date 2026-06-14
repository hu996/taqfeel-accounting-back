using System.Security.Claims;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class CurrentSessionService : ICurrentSessionService
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public CurrentSessionService(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null || user.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("An authenticated session is required.");
            }

            return user;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            return httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
        }
    }

    public Guid UserId
    {
        get
        {
            return GetRequiredGuid(SessionClaimNames.UserId);
        }
    }

    public string UserName
    {
        get
        {
            return GetRequiredString(SessionClaimNames.UserName);
        }
    }

    public string Email
    {
        get
        {
            return GetRequiredString(SessionClaimNames.Email);
        }
    }

    public Guid? EmployeeId
    {
        get
        {
            return GetOptionalGuid(SessionClaimNames.EmployeeId);
        }
    }

    public string? EmployeeCode
    {
        get
        {
            return GetOptionalString(SessionClaimNames.EmployeeCode);
        }
    }

    public string? EmployeeName
    {
        get
        {
            return GetOptionalString(SessionClaimNames.EmployeeName);
        }
    }

    public Guid TenantId
    {
        get
        {
            return GetRequiredGuid(SessionClaimNames.TenantId);
        }
    }

    public Guid CompanyId
    {
        get
        {
            return GetRequiredGuid(SessionClaimNames.CompanyId);
        }
    }

    public string CompanyCode
    {
        get
        {
            return GetRequiredString(SessionClaimNames.CompanyCode);
        }
    }

    public string CompanyNameAr
    {
        get
        {
            return GetRequiredString(SessionClaimNames.CompanyNameAr);
        }
    }

    public string CompanyNameEn
    {
        get
        {
            return GetRequiredString(SessionClaimNames.CompanyNameEn);
        }
    }

    public string? CompanyLogoUrl
    {
        get
        {
            return GetOptionalString(SessionClaimNames.CompanyLogoUrl);
        }
    }

    public Guid ActiveFinancialYearId
    {
        get
        {
            return GetRequiredGuid(SessionClaimNames.ActiveFinancialYearId);
        }
    }

    public string ActiveFinancialYearCode
    {
        get
        {
            return GetRequiredString(SessionClaimNames.ActiveFinancialYearCode);
        }
    }

    public string ActiveFinancialYearName
    {
        get
        {
            return GetRequiredString(SessionClaimNames.ActiveFinancialYearName);
        }
    }

    public Guid? ActiveAccountingPeriodId
    {
        get
        {
            return GetOptionalGuid(SessionClaimNames.ActiveAccountingPeriodId);
        }
    }

    public string? ActiveAccountingPeriodCode
    {
        get
        {
            return GetOptionalString(SessionClaimNames.ActiveAccountingPeriodCode);
        }
    }

    public string? ActiveAccountingPeriodName
    {
        get
        {
            return GetOptionalString(SessionClaimNames.ActiveAccountingPeriodName);
        }
    }

    public Guid? BranchId
    {
        get
        {
            return GetOptionalGuid(SessionClaimNames.BranchId);
        }
    }

    public Guid? DepartmentId
    {
        get
        {
            return GetOptionalGuid(SessionClaimNames.DepartmentId);
        }
    }

    public bool IsSuperAdmin
    {
        get
        {
            var value = GetRequiredString(SessionClaimNames.IsSuperAdmin);
            return bool.TryParse(value, out var parsed) && parsed;
        }
    }

    public string Language
    {
        get
        {
            return GetRequiredString(SessionClaimNames.Language);
        }
    }

    public IReadOnlyList<string> Roles
    {
        get
        {
            return GetValues(SessionClaimNames.Role);
        }
    }

    public IReadOnlyList<string> Permissions
    {
        get
        {
            return GetValues(SessionClaimNames.Permission);
        }
    }

    public IReadOnlyList<string> EnabledModules
    {
        get
        {
            return GetValues(SessionClaimNames.Module);
        }
    }

    public bool HasRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasModule(string module)
    {
        return EnabledModules.Contains(module, StringComparer.OrdinalIgnoreCase);
    }

    private Guid GetRequiredGuid(string claimType)
    {
        var value = GetRequiredString(claimType);
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            throw new UnauthorizedAccessException($"JWT claim '{claimType}' is invalid.");
        }

        return id;
    }

    private Guid? GetOptionalGuid(string claimType)
    {
        var value = GetOptionalString(claimType);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var id))
        {
            throw new UnauthorizedAccessException($"JWT claim '{claimType}' is invalid.");
        }

        return id;
    }

    private string GetRequiredString(string claimType)
    {
        var value = User.FindFirstValue(claimType);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UnauthorizedAccessException($"Required JWT claim '{claimType}' is missing.");
        }

        return value;
    }

    private string? GetOptionalString(string claimType)
    {
        return User.FindFirstValue(claimType);
    }

    private IReadOnlyList<string> GetValues(string claimType)
    {
        return User.FindAll(claimType)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly ICurrentSessionService currentSession;

    public CurrentUserService(ICurrentSessionService currentSession)
    {
        this.currentSession = currentSession;
    }

    public Guid? UserId
    {
        get
        {
            if (!currentSession.IsAuthenticated)
            {
                return null;
            }

            return currentSession.UserId;
        }
    }

    public string? Email
    {
        get
        {
            if (!currentSession.IsAuthenticated)
            {
                return null;
            }

            return currentSession.Email;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            return currentSession.IsAuthenticated;
        }
    }

    public bool IsSuperAdmin
    {
        get
        {
            return currentSession.IsAuthenticated && currentSession.IsSuperAdmin;
        }
    }

    public bool IsAccountingOfficeAdmin
    {
        get
        {
            return currentSession.IsAuthenticated &&
                   currentSession.HasRole(
                       AccountingSaaS.Domain.Constants.Roles.AccountingOfficeAdmin);
        }
    }

    public IReadOnlyList<string> Roles
    {
        get
        {
            if (!currentSession.IsAuthenticated)
            {
                return [];
            }

            return currentSession.Roles;
        }
    }

    public IReadOnlyList<string> Permissions
    {
        get
        {
            if (!currentSession.IsAuthenticated)
            {
                return [];
            }

            return currentSession.Permissions;
        }
    }
}

public sealed class CurrentTenantService : ICurrentTenantService
{
    private readonly ICurrentSessionService currentSession;
    private Guid? internalTenantId;

    public CurrentTenantService(ICurrentSessionService currentSession)
    {
        this.currentSession = currentSession;
    }

    public Guid? TenantId
    {
        get
        {
            if (internalTenantId.HasValue)
            {
                return internalTenantId;
            }

            if (!currentSession.IsAuthenticated)
            {
                return null;
            }

            return currentSession.TenantId;
        }
    }

    public bool IsTenantSelected
    {
        get
        {
            return TenantId.HasValue;
        }
    }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        internalTenantId = tenantId;
    }

    public void Clear()
    {
        internalTenantId = null;
    }
}
