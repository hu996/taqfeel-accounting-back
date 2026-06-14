namespace AccountingSaaS.Application.Interfaces;

public interface ICurrentSessionService
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string UserName { get; }
    string Email { get; }
    Guid? EmployeeId { get; }
    string? EmployeeCode { get; }
    string? EmployeeName { get; }
    Guid TenantId { get; }
    Guid CompanyId { get; }
    string CompanyCode { get; }
    string CompanyNameAr { get; }
    string CompanyNameEn { get; }
    string? CompanyLogoUrl { get; }
    Guid ActiveFinancialYearId { get; }
    string ActiveFinancialYearCode { get; }
    string ActiveFinancialYearName { get; }
    Guid? ActiveAccountingPeriodId { get; }
    string? ActiveAccountingPeriodCode { get; }
    string? ActiveAccountingPeriodName { get; }
    Guid? BranchId { get; }
    Guid? DepartmentId { get; }
    bool IsSuperAdmin { get; }
    string Language { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    IReadOnlyList<string> EnabledModules { get; }
    bool HasRole(string role);
    bool HasPermission(string permission);
    bool HasModule(string module);
}

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

public static class SessionClaimNames
{
    public const string UserId = "user_id";
    public const string UserName = "user_name";
    public const string Email = "email";
    public const string EmployeeId = "employee_id";
    public const string EmployeeCode = "employee_code";
    public const string EmployeeName = "employee_name";
    public const string TenantId = "tenant_id";
    public const string CompanyId = "company_id";
    public const string CompanyCode = "company_code";
    public const string CompanyNameAr = "company_name_ar";
    public const string CompanyNameEn = "company_name_en";
    public const string CompanyLogoUrl = "company_logo_url";
    public const string ActiveFinancialYearId = "active_financial_year_id";
    public const string ActiveFinancialYearCode = "active_financial_year_code";
    public const string ActiveFinancialYearName = "active_financial_year_name";
    public const string ActiveAccountingPeriodId = "active_accounting_period_id";
    public const string ActiveAccountingPeriodCode = "active_accounting_period_code";
    public const string ActiveAccountingPeriodName = "active_accounting_period_name";
    public const string BranchId = "branch_id";
    public const string DepartmentId = "department_id";
    public const string IsSuperAdmin = "is_super_admin";
    public const string Language = "language";
    public const string Permission = "permission";
    public const string Role = "role";
    public const string Module = "module";
}
