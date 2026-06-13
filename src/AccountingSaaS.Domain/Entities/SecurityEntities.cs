namespace AccountingSaaS.Domain.Entities;

public sealed class Permission
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; } = default!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}

public sealed class UserTenantAccess
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = default!;
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;
}

public sealed class ReviewerTenantAssignment
{
    public Guid ReviewerUserId { get; set; }
    public ApplicationUser ReviewerUser { get; set; } = default!;
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class NumberSequence
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string SequenceKey { get; set; } = string.Empty;
    public long LastNumber { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = default!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
