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
    public DbSet<Tenant> Tenants { get { return Set<Tenant>(); } }
    public DbSet<Permission> Permissions { get { return Set<Permission>(); } }
    public DbSet<RolePermission> RolePermissions { get { return Set<RolePermission>(); } }
    public DbSet<UserTenantAccess> UserTenantAccesses { get { return Set<UserTenantAccess>(); } }
    public DbSet<ReviewerTenantAssignment> ReviewerTenantAssignments { get { return Set<ReviewerTenantAssignment>(); } }
    public DbSet<NumberSequence> NumberSequences { get { return Set<NumberSequence>(); } }
    public DbSet<RefreshToken> RefreshTokens { get { return Set<RefreshToken>(); } }
    public DbSet<AuditLog> AuditLogs { get { return Set<AuditLog>(); } }
    public DbSet<PasswordHistory> PasswordHistories { get { return Set<PasswordHistory>(); } }
    public DbSet<FinancialYear> FinancialYears { get { return Set<FinancialYear>(); } }
    public DbSet<AccountingPeriod> AccountingPeriods { get { return Set<AccountingPeriod>(); } }
    public DbSet<Account> Accounts { get { return Set<Account>(); } }
    public DbSet<CostCenter> CostCenters { get { return Set<CostCenter>(); } }
    public DbSet<JournalEntry> JournalEntries { get { return Set<JournalEntry>(); } }
    public DbSet<JournalEntryLine> JournalEntryLines { get { return Set<JournalEntryLine>(); } }
    public DbSet<Document> Documents { get { return Set<Document>(); } }
    public DbSet<ClosingChecklistTemplate> ClosingChecklistTemplates { get { return Set<ClosingChecklistTemplate>(); } }
    public DbSet<ClosingChecklistTemplateItem> ClosingChecklistTemplateItems { get { return Set<ClosingChecklistTemplateItem>(); } }
    public DbSet<ClosingTask> ClosingTasks { get { return Set<ClosingTask>(); } }
    public DbSet<ClosingSubmission> ClosingSubmissions { get { return Set<ClosingSubmission>(); } }
    public DbSet<ImportBatch> ImportBatches { get { return Set<ImportBatch>(); } }
    public DbSet<ImportBatchRow> ImportBatchRows { get { return Set<ImportBatchRow>(); } }
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get { return Set<WorkflowDefinition>(); } }
    public DbSet<TenantModule> TenantModules { get { return Set<TenantModule>(); } }
    public DbSet<WorkflowStep> WorkflowSteps { get { return Set<WorkflowStep>(); } }
    public DbSet<WorkflowAction> WorkflowActions { get { return Set<WorkflowAction>(); } }
    public DbSet<Notification> Notifications { get { return Set<Notification>(); } }
    public DbSet<ActivityLog> ActivityLogs { get { return Set<ActivityLog>(); } }
    public DbSet<JournalEntryVersion> JournalEntryVersions { get { return Set<JournalEntryVersion>(); } }
    public DbSet<EntityComment> EntityComments { get { return Set<EntityComment>(); } }
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions { get { return Set<CustomFieldDefinition>(); } }
    public DbSet<CustomFieldValue> CustomFieldValues { get { return Set<CustomFieldValue>(); } }
    public DbSet<DocumentNumberTemplate> DocumentNumberTemplates { get { return Set<DocumentNumberTemplate>(); } }
    public DbSet<OpeningBalanceBatch> OpeningBalanceBatches { get { return Set<OpeningBalanceBatch>(); } }
    public DbSet<OpeningBalanceLine> OpeningBalanceLines { get { return Set<OpeningBalanceLine>(); } }
    public DbSet<BankAccount> BankAccounts { get { return Set<BankAccount>(); } }
    public DbSet<BankStatement> BankStatements { get { return Set<BankStatement>(); } }
    public DbSet<BankReconciliation> BankReconciliations { get { return Set<BankReconciliation>(); } }
    public DbSet<BankReconciliationMatch> BankReconciliationMatches { get { return Set<BankReconciliationMatch>(); } }
    public DbSet<FixedAsset> FixedAssets { get { return Set<FixedAsset>(); } }
    public DbSet<AssetDepreciationRun> AssetDepreciationRuns { get { return Set<AssetDepreciationRun>(); } }
    public DbSet<AssetDepreciationLine> AssetDepreciationLines { get { return Set<AssetDepreciationLine>(); } }
    public DbSet<RecurringJournalEntry> RecurringJournalEntries { get { return Set<RecurringJournalEntry>(); } }
    public DbSet<RecurringJournalEntryLine> RecurringJournalEntryLines { get { return Set<RecurringJournalEntryLine>(); } }
    public DbSet<GeneratedRecurringEntry> GeneratedRecurringEntries { get { return Set<GeneratedRecurringEntry>(); } }
    public DbSet<ClosingCheck> ClosingChecks { get { return Set<ClosingCheck>(); } }
    public DbSet<ReportDefinition> ReportDefinitions { get { return Set<ReportDefinition>(); } }
    public DbSet<Customer> Customers { get { return Set<Customer>(); } }
    public DbSet<Vendor> Vendors { get { return Set<Vendor>(); } }
    public DbSet<Employee> Employees { get { return Set<Employee>(); } }

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
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.DeletedByUserId = currentUser.UserId;
            }

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
            var selectedTenantId = currentTenant.TenantId;

            //var entityName = entry.Entity.GetType().Name;
            //var state = entry.State.ToString();
            //var entityTenantId = entry.Entity.TenantId;

            ////var selectedTenantId = currentTenant.TenantId;

            //if (!selectedTenantId.HasValue)
            //{
            //    throw new UnauthorizedAccessException(
            //        $"A tenant session is required. Entity={entityName}, State={state}, EntityTenantId={entityTenantId}");
            //}


            if (!selectedTenantId.HasValue)
            {
    //            var tenantEntries = ChangeTracker.Entries<ITenantEntity>()
    //.Where(x => x.State == EntityState.Added || x.State == EntityState.Modified || x.State == EntityState.Deleted)
    //.Select(x => new
    //{
    //    EntityName = x.Entity.GetType().Name,
    //    State = x.State.ToString(),
    //    TenantId = x.Entity.TenantId
    //})
    //.ToList();

    //            var details = string.Join(", ", tenantEntries.Select(x =>
    //                $"{x.EntityName} - {x.State} - {x.TenantId}"));

    //            throw new UnauthorizedAccessException(
    //                $"A tenant session is required for tenant-owned changes. Entries: {details}");
    //            throw new UnauthorizedAccessException("A tenant session is required for tenant-owned changes.");
            }

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.TenantId != Guid.Empty && entry.Entity.TenantId != selectedTenantId.Value)
                {
                    throw new InvalidOperationException("TenantId cannot be supplied or overridden for tenant-owned entities.");
                }

                entry.Entity.TenantId = selectedTenantId.Value;
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
                entry.Entity.TenantId = selectedTenantId.Value;
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

    public Guid? CurrentTenantId
    {
        get
        {
            return currentTenant.TenantId;
        }
    }
}
