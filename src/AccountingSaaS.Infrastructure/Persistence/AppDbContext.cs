using System.Linq.Expressions;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserTenantAccess> UserTenantAccesses => Set<UserTenantAccess>();
    public DbSet<ReviewerTenantAssignment> ReviewerTenantAssignments => Set<ReviewerTenantAssignment>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FinancialYear> FinancialYears => Set<FinancialYear>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ClosingChecklistTemplate> ClosingChecklistTemplates => Set<ClosingChecklistTemplate>();
    public DbSet<ClosingChecklistTemplateItem> ClosingChecklistTemplateItems => Set<ClosingChecklistTemplateItem>();
    public DbSet<ClosingTask> ClosingTasks => Set<ClosingTask>();
    public DbSet<ClosingSubmission> ClosingSubmissions => Set<ClosingSubmission>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportBatchRow> ImportBatchRows => Set<ImportBatchRow>();

    public bool DisableTenantFilter { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplySoftDeleteFilters(builder);
        ApplyTenantFilters(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndTenantRules();

        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditAndTenantRules()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.Id = entry.Entity.Id == Guid.Empty ? Guid.NewGuid() : entry.Entity.Id;
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedByUserId = currentUser.UserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.CreatedAt).IsModified = false;
                entry.Property(x => x.CreatedByUserId).IsModified = false;
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByUserId = currentUser.UserId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            var selectedTenantId = currentTenant.TenantId ?? TenantDefaults.DefaultTenantId;

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.TenantId != Guid.Empty && entry.Entity.TenantId != selectedTenantId)
                {
                    throw new InvalidOperationException("TenantId cannot be supplied or overridden for tenant-owned entities.");
                }

                entry.Entity.TenantId = selectedTenantId;
            }

            if (entry.State == EntityState.Modified)
            {
                var tenantProperty = entry.Property(nameof(ITenantEntity.TenantId));

                if (tenantProperty.IsModified && !Equals(tenantProperty.OriginalValue, tenantProperty.CurrentValue))
                {
                    throw new InvalidOperationException("TenantId cannot be changed after creation.");
                }

                tenantProperty.IsModified = false;
            }

            if (entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = selectedTenantId;
            }
        }
    }

    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");

            var body = Expression.Equal(
                Expression.Property(parameter, nameof(BaseEntity.IsDeleted)),
                Expression.Constant(false));

            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    private void ApplyTenantFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");

            var tenantProperty = Expression.Property(
                parameter,
                nameof(ITenantEntity.TenantId));

            var tenantId = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentTenantId));

            var disableTenantFilter = Expression.Property(
                Expression.Constant(this),
                nameof(DisableTenantFilter));

            Expression tenantBody = Expression.OrElse(
                disableTenantFilter,
                Expression.AndAlso(
                    Expression.NotEqual(
                        tenantId,
                        Expression.Constant(null, typeof(Guid?))),
                    Expression.Equal(
                        Expression.Convert(tenantProperty, typeof(Guid?)),
                        tenantId)));

            Expression body = tenantBody;

            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                body = Expression.AndAlso(
                    Expression.Equal(
                        Expression.Property(parameter, nameof(BaseEntity.IsDeleted)),
                        Expression.Constant(false)),
                    body);
            }

            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    public Guid? CurrentTenantId => currentTenant.TenantId ?? TenantDefaults.DefaultTenantId;
}
