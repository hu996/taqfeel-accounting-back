using Microsoft.AspNetCore.Identity;

namespace AccountingSaaS.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public long UserNo { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? ActiveFinancialYearId { get; set; }
    public string Language { get; set; } = "ar";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<UserTenantAccess> TenantAccesses { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<PasswordHistory> PasswordHistory { get; set; } = [];
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
