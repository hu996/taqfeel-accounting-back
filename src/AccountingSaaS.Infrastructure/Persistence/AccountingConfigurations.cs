using AccountingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingSaaS.Infrastructure.Persistence;

public sealed class FinancialYearConfiguration : IEntityTypeConfiguration<FinancialYear>
{
    public void Configure(EntityTypeBuilder<FinancialYear> builder)
    {
        builder.Property(x => x.YearName).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.YearName }).IsUnique();
        builder.HasIndex(x => x.TenantId);
    }
}

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.Property(x => x.PeriodName).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.FinancialYearId });
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.AccountNo }).IsUnique();
        builder.Property(x => x.NameAr).HasMaxLength(200);
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasOne(x => x.ParentAccount).WithMany().HasForeignKey(x => x.ParentAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CostCenterNo }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.Property(x => x.EntryNumber).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ReviewReason).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.JournalEntryNo }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.WorkflowStatus, x.AssignedReviewerUserId });
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TotalDebit).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TotalCredit).HasColumnType("decimal(18,2)");
        builder.HasIndex(x => new { x.TenantId, x.FinancialYearId });
        builder.HasIndex(x => new { x.TenantId, x.AccountingPeriodId });
        builder.HasIndex(x => new { x.TenantId, x.EntryDate });
        builder.HasIndex(x => new { x.TenantId, x.FinancialYearId, x.EntryNumber }).IsUnique();
        builder.HasOne(x => x.FinancialYear).WithMany().HasForeignKey(x => x.FinancialYearId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AccountingPeriod).WithMany().HasForeignKey(x => x.AccountingPeriodId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.Lines).WithOne(x => x.JournalEntry).HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.Property(x => x.Debit).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Credit).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.AccountId });
        builder.HasIndex(x => new { x.TenantId, x.CostCenterId });
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CostCenter).WithMany().HasForeignKey(x => x.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.DocumentNo }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.WorkflowStatus, x.AssignedReviewerUserId });
        builder.Property(x => x.StoredFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.RelatedEntityName).HasMaxLength(120);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.FinancialYearId });
        builder.HasIndex(x => new { x.TenantId, x.AccountingPeriodId });
    }
}

public sealed class ClosingChecklistTemplateConfiguration : IEntityTypeConfiguration<ClosingChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ClosingChecklistTemplate> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.IsDefault });
        builder.HasMany(x => x.Items).WithOne(x => x.Template).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClosingChecklistTemplateItemConfiguration : IEntityTypeConfiguration<ClosingChecklistTemplateItem>
{
    public void Configure(EntityTypeBuilder<ClosingChecklistTemplateItem> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.SortOrder });
    }
}

public sealed class ClosingTaskConfiguration : IEntityTypeConfiguration<ClosingTask>
{
    public void Configure(EntityTypeBuilder<ClosingTask> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.FinancialYearId });
        builder.HasIndex(x => new { x.TenantId, x.AccountingPeriodId });
    }
}

public sealed class ClosingSubmissionConfiguration : IEntityTypeConfiguration<ClosingSubmission>
{
    public void Configure(EntityTypeBuilder<ClosingSubmission> builder)
    {
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.ReopenReason).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.AssignedReviewerUserId });
        builder.HasIndex(x => new { x.TenantId, x.AccountingPeriodId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.FinancialYearId });
    }
}
