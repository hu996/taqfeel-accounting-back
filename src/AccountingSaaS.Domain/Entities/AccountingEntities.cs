using AccountingSaaS.Domain.Enums;

namespace AccountingSaaS.Domain.Entities;

public sealed class FinancialYear : TenantEntity
{
    public string YearName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public FinancialYearStatus Status { get; set; } = FinancialYearStatus.Open;
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public ICollection<AccountingPeriod> Periods { get; set; } = [];
}

public sealed class AccountingPeriod : TenantEntity
{
    public Guid FinancialYearId { get; set; }
    public FinancialYear FinancialYear { get; set; } = default!;
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;
    public DateTimeOffset? LockedAt { get; set; }
    public Guid? LockedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset? ReopenedAt { get; set; }
    public Guid? ReopenedByUserId { get; set; }
}

public sealed class Account : TenantEntity
{
    public long AccountNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public NormalBalance NormalBalance { get; set; }
    public Guid? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }
    public bool IsPostingAccount { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CostCenter : TenantEntity
{
    public long CostCenterNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class JournalEntry : TenantEntity
{
    public long JournalEntryNo { get; set; }
    public Guid FinancialYearId { get; set; }
    public FinancialYear FinancialYear { get; set; } = default!;
    public Guid AccountingPeriodId { get; set; }
    public AccountingPeriod AccountingPeriod { get; set; } = default!;
    public string EntryNumber { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public WorkflowStatus WorkflowStatus { get; set; } = WorkflowStatus.Draft;
    public Guid? AssignedReviewerUserId { get; set; }
    public string? ReviewReason { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid? ReversedByUserId { get; set; }
    public string? ReversalReason { get; set; }
    public ICollection<JournalEntryLine> Lines { get; set; } = [];
}

public sealed class JournalEntryLine : TenantEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = default!;
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = default!;
    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public sealed class Document : TenantEntity
{
    public long DocumentNo { get; set; }
    public Guid? FinancialYearId { get; set; }
    public Guid? AccountingPeriodId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string? RelatedEntityName { get; set; }
    public string? RelatedEntityId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string? Notes { get; set; }
    public WorkflowStatus WorkflowStatus { get; set; } = WorkflowStatus.Draft;
    public Guid? AssignedReviewerUserId { get; set; }
}

public sealed class ClosingChecklistTemplate : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ClosingChecklistTemplateItem> Items { get; set; } = [];
}

public sealed class ClosingChecklistTemplateItem : TenantEntity
{
    public Guid TemplateId { get; set; }
    public ClosingChecklistTemplate Template { get; set; } = default!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
}

public sealed class ClosingTask : TenantEntity
{
    public Guid FinancialYearId { get; set; }
    public Guid AccountingPeriodId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public ClosingTaskStatus Status { get; set; } = ClosingTaskStatus.Pending;
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class ClosingSubmission : TenantEntity
{
    public Guid FinancialYearId { get; set; }
    public Guid AccountingPeriodId { get; set; }
    public ClosingSubmissionStatus Status { get; set; } = ClosingSubmissionStatus.Draft;
    public Guid? AssignedReviewerUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset? ReopenedAt { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public string? ReopenReason { get; set; }
    public string? Notes { get; set; }
}
