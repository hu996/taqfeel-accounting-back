using AccountingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingSaaS.Infrastructure.Persistence;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CompanyNameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyNameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyLogoUrl).HasMaxLength(500);
        builder.HasIndex(x => x.CompanyCode).IsUnique();
        builder.HasIndex(x => x.TenantNo).IsUnique();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => x.CompanyName);
        builder.HasIndex(x => x.IsDeleted);
    }
}

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId })
            .IsUnique()
            .HasFilter("[EmployeeId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => x.UserNo).IsUnique();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class ReviewerTenantAssignmentConfiguration : IEntityTypeConfiguration<ReviewerTenantAssignment>
{
    public void Configure(EntityTypeBuilder<ReviewerTenantAssignment> builder)
    {
        builder.HasKey(x => new { x.ReviewerUserId, x.TenantId });
        builder.HasOne(x => x.ReviewerUser).WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.Property(x => x.SequenceKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SequenceKey }).IsUnique();
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
        builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
    }
}

public sealed class UserTenantAccessConfiguration : IEntityTypeConfiguration<UserTenantAccess>
{
    public void Configure(EntityTypeBuilder<UserTenantAccess> builder)
    {
        builder.HasKey(x => new { x.UserId, x.TenantId });
        builder.HasOne(x => x.User).WithMany(x => x.TenantAccesses).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(120);
        builder.Property(x => x.EntityId).HasMaxLength(80);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
