namespace AccountingSaaS.Application.DTOs;

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeeName { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public Guid ActiveFinancialYearId { get; set; }
    public string ActiveFinancialYearCode { get; set; } = string.Empty;
    public string ActiveFinancialYearName { get; set; } = string.Empty;
    public Guid? ActiveAccountingPeriodId { get; set; }
    public string? ActiveAccountingPeriodCode { get; set; }
    public string? ActiveAccountingPeriodName { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool MustChangePassword { get; set; }
    public string Language { get; set; } = "ar";
    public List<string> Roles { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public List<string> EnabledModules { get; set; } = [];
}

public sealed class SessionContextDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeeName { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public Guid ActiveFinancialYearId { get; set; }
    public string ActiveFinancialYearCode { get; set; } = string.Empty;
    public string ActiveFinancialYearName { get; set; } = string.Empty;
    public Guid? ActiveAccountingPeriodId { get; set; }
    public string? ActiveAccountingPeriodCode { get; set; }
    public string? ActiveAccountingPeriodName { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsSuperAdmin { get; set; }
    public string Language { get; set; } = "ar";
    public List<string> Roles { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public List<string> EnabledModules { get; set; } = [];
}

public sealed class SwitchActiveFinancialYearRequest
{
    public Guid FinancialYearId { get; set; }
}
