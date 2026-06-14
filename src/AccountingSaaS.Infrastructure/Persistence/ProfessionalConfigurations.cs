using AccountingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingSaaS.Infrastructure.Persistence;

public sealed class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    public void Configure(EntityTypeBuilder<TenantModule> b)
    {
        b.Property(x => x.ModuleKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.ModuleKey }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.IsActive });
        b.HasMany(x => x.Steps).WithOne(x => x.WorkflowDefinition)
            .HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> b)
    {
        b.Property(x => x.StepNameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.StepNameEn).HasMaxLength(200).IsRequired();
        b.Property(x => x.RequiredPermission).HasMaxLength(150);
        b.HasIndex(x => new { x.TenantId, x.WorkflowDefinitionId, x.StepOrder }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class WorkflowActionConfiguration : IEntityTypeConfiguration<WorkflowAction>
{
    public void Configure(EntityTypeBuilder<WorkflowAction> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.FromStatus).HasMaxLength(80).IsRequired();
        b.Property(x => x.ToStatus).HasMaxLength(80).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.ActionDate });
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.TitleAr).HasMaxLength(250).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(250).IsRequired();
        b.Property(x => x.MessageAr).HasMaxLength(1500).IsRequired();
        b.Property(x => x.MessageEn).HasMaxLength(1500).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.IsRead, x.CreatedAt });
    }
}

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> b)
    {
        b.Property(x => x.Action).HasMaxLength(120).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.TitleAr).HasMaxLength(250).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(250).IsRequired();
        b.Property(x => x.DescriptionAr).HasMaxLength(1500).IsRequired();
        b.Property(x => x.DescriptionEn).HasMaxLength(1500).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

public sealed class JournalEntryVersionConfiguration : IEntityTypeConfiguration<JournalEntryVersion>
{
    public void Configure(EntityTypeBuilder<JournalEntryVersion> b)
    {
        b.Property(x => x.SnapshotJson).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.JournalEntryId, x.VersionNo }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class EntityCommentConfiguration : IEntityTypeConfiguration<EntityComment>
{
    public void Configure(EntityTypeBuilder<EntityComment> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.CommentText).HasMaxLength(4000).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.CreatedAt });
    }
}

public sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.FieldKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.FieldNameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.FieldNameEn).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.FieldKey }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasMaxLength(4000);
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.CustomFieldDefinitionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DocumentNumberTemplateConfiguration : IEntityTypeConfiguration<DocumentNumberTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentNumberTemplate> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Template).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.IsActive });
    }
}

public sealed class OpeningBalanceBatchConfiguration : IEntityTypeConfiguration<OpeningBalanceBatch>
{
    public void Configure(EntityTypeBuilder<OpeningBalanceBatch> b)
    {
        b.Property(x => x.BatchNo).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.BatchNo }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasMany(x => x.Lines).WithOne(x => x.Batch).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class OpeningBalanceLineConfiguration : IEntityTypeConfiguration<OpeningBalanceLine>
{
    public void Configure(EntityTypeBuilder<OpeningBalanceLine> b)
    {
        b.Property(x => x.Debit).HasColumnType("decimal(18,2)");
        b.Property(x => x.Credit).HasColumnType("decimal(18,2)");
    }
}

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> b)
    {
        b.Property(x => x.BankName).HasMaxLength(200).IsRequired();
        b.Property(x => x.AccountNumber).HasMaxLength(100).IsRequired();
        b.Property(x => x.Iban).HasMaxLength(50);
        b.HasIndex(x => new { x.TenantId, x.AccountNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class BankStatementConfiguration : IEntityTypeConfiguration<BankStatement>
{
    public void Configure(EntityTypeBuilder<BankStatement> b)
    {
        b.Property(x => x.Debit).HasColumnType("decimal(18,2)");
        b.Property(x => x.Credit).HasColumnType("decimal(18,2)");
        b.HasIndex(x => new { x.TenantId, x.BankAccountId, x.StatementDate, x.IsMatched });
    }
}

public sealed class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> b) =>
        b.HasIndex(x => new { x.TenantId, x.BankAccountId, x.AccountingPeriodId }).IsUnique().HasFilter("[IsDeleted] = 0");
}

public sealed class BankReconciliationMatchConfiguration : IEntityTypeConfiguration<BankReconciliationMatch>
{
    public void Configure(EntityTypeBuilder<BankReconciliationMatch> b)
    {
        b.Property(x => x.MatchedAmount).HasColumnType("decimal(18,2)");
        b.HasIndex(x => new { x.TenantId, x.BankStatementId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.TenantId, x.JournalEntryLineId });
    }
}

public sealed class FixedAssetConfiguration : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> b)
    {
        b.Property(x => x.AssetCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.PurchaseCost).HasColumnType("decimal(18,2)");
        b.Property(x => x.SalvageValue).HasColumnType("decimal(18,2)");
        b.Property(x => x.AccumulatedDepreciation).HasColumnType("decimal(18,2)");
        b.Property(x => x.BookValue).HasColumnType("decimal(18,2)");
        b.HasIndex(x => new { x.TenantId, x.AssetCode }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class AssetDepreciationRunConfiguration : IEntityTypeConfiguration<AssetDepreciationRun>
{
    public void Configure(EntityTypeBuilder<AssetDepreciationRun> b)
    {
        b.HasIndex(x => new { x.TenantId, x.AccountingPeriodId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasMany(x => x.Lines).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AssetDepreciationLineConfiguration : IEntityTypeConfiguration<AssetDepreciationLine>
{
    public void Configure(EntityTypeBuilder<AssetDepreciationLine> b) =>
        b.Property(x => x.DepreciationAmount).HasColumnType("decimal(18,2)");
}

public sealed class RecurringJournalEntryConfiguration : IEntityTypeConfiguration<RecurringJournalEntry>
{
    public void Configure(EntityTypeBuilder<RecurringJournalEntry> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasMany(x => x.Lines).WithOne(x => x.RecurringJournalEntry)
            .HasForeignKey(x => x.RecurringJournalEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RecurringJournalEntryLineConfiguration : IEntityTypeConfiguration<RecurringJournalEntryLine>
{
    public void Configure(EntityTypeBuilder<RecurringJournalEntryLine> b)
    {
        b.Property(x => x.Debit).HasColumnType("decimal(18,2)");
        b.Property(x => x.Credit).HasColumnType("decimal(18,2)");
    }
}

public sealed class GeneratedRecurringEntryConfiguration : IEntityTypeConfiguration<GeneratedRecurringEntry>
{
    public void Configure(EntityTypeBuilder<GeneratedRecurringEntry> b) =>
        b.HasIndex(x => new { x.TenantId, x.RecurringJournalEntryId, x.AccountingPeriodId }).IsUnique().HasFilter("[IsDeleted] = 0");
}

public sealed class ClosingCheckConfiguration : IEntityTypeConfiguration<ClosingCheck>
{
    public void Configure(EntityTypeBuilder<ClosingCheck> b)
    {
        b.Property(x => x.CheckKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.AccountingPeriodId, x.CheckKey }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> b)
    {
        b.Property(x => x.ReportName).HasMaxLength(200).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.ReportName });
    }
}

public sealed class BusinessPartyConfiguration :
    IEntityTypeConfiguration<Customer>,
    IEntityTypeConfiguration<Vendor>,
    IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Customer> b) => ConfigureParty(b);
    public void Configure(EntityTypeBuilder<Vendor> b) => ConfigureParty(b);
    public void Configure(EntityTypeBuilder<Employee> b) => ConfigureParty(b);

    private static void ConfigureParty<T>(EntityTypeBuilder<T> b) where T : BusinessParty
    {
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> b)
    {
        b.Property(x => x.PasswordHash).IsRequired();
        b.HasOne(x => x.User).WithMany(x => x.PasswordHistory).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
