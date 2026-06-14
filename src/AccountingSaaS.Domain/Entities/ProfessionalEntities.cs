using AccountingSaaS.Domain.Enums;

namespace AccountingSaaS.Domain.Entities;

public sealed class TenantModule : TenantEntity
{
    public string ModuleKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public sealed class WorkflowDefinition : TenantEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<WorkflowStep> Steps { get; set; } = [];
}

public sealed class WorkflowStep : TenantEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = default!;
    public int StepOrder { get; set; }
    public string StepNameAr { get; set; } = string.Empty;
    public string StepNameEn { get; set; } = string.Empty;
    public Guid? RequiredRoleId { get; set; }
    public string? RequiredPermission { get; set; }
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanReturn { get; set; }
    public bool IsFinalApproval { get; set; }
}

public sealed class WorkflowAction : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowStepId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public WorkflowActionType Action { get; set; }
    public Guid ActionByUserId { get; set; }
    public DateTimeOffset ActionDate { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public sealed class Notification : TenantEntity
{
    public Guid UserId { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string MessageAr { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class ActivityLog : TenantEntity
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
}

public sealed class JournalEntryVersion : TenantEntity
{
    public Guid JournalEntryId { get; set; }
    public int VersionNo { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string? ChangeReason { get; set; }
}

public sealed class EntityComment : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public sealed class CustomFieldDefinition : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public string FieldKey { get; set; } = string.Empty;
    public string FieldNameAr { get; set; } = string.Empty;
    public string FieldNameEn { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string? OptionsJson { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CustomFieldValue : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid CustomFieldDefinitionId { get; set; }
    public string? Value { get; set; }
}

public sealed class DocumentNumberTemplate : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public ResetPeriod ResetPeriod { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class OpeningBalanceBatch : TenantEntity
{
    public Guid FinancialYearId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public OpeningBalanceStatus Status { get; set; } = OpeningBalanceStatus.Draft;
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowStepId { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public ICollection<OpeningBalanceLine> Lines { get; set; } = [];
}

public sealed class OpeningBalanceLine : TenantEntity
{
    public Guid BatchId { get; set; }
    public OpeningBalanceBatch Batch { get; set; } = default!;
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? Notes { get; set; }
}

public sealed class BankAccount : TenantEntity
{
    public Guid AccountId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Iban { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BankStatement : TenantEntity
{
    public Guid BankAccountId { get; set; }
    public DateOnly StatementDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? ReferenceNo { get; set; }
    public bool IsMatched { get; set; }
}

public sealed class BankReconciliation : TenantEntity
{
    public Guid BankAccountId { get; set; }
    public Guid AccountingPeriodId { get; set; }
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Draft;
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class BankReconciliationMatch : TenantEntity
{
    public Guid ReconciliationId { get; set; }
    public Guid BankStatementId { get; set; }
    public Guid JournalEntryLineId { get; set; }
    public BankMatchType MatchType { get; set; }
    public decimal MatchedAmount { get; set; }
    public Guid MatchedByUserId { get; set; }
    public DateTimeOffset MatchedAt { get; set; }
}

public sealed class FixedAsset : TenantEntity
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetNameAr { get; set; } = string.Empty;
    public string AssetNameEn { get; set; } = string.Empty;
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal SalvageValue { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal BookValue { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Active;
}

public sealed class AssetDepreciationRun : TenantEntity
{
    public Guid AccountingPeriodId { get; set; }
    public DateOnly RunDate { get; set; }
    public DepreciationRunStatus Status { get; set; } = DepreciationRunStatus.Draft;
    public Guid? ApprovedByUserId { get; set; }
    public ICollection<AssetDepreciationLine> Lines { get; set; } = [];
}

public sealed class AssetDepreciationLine : TenantEntity
{
    public Guid RunId { get; set; }
    public AssetDepreciationRun Run { get; set; } = default!;
    public Guid AssetId { get; set; }
    public decimal DepreciationAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public sealed class RecurringJournalEntry : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public RecurringFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly NextRunDate { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RecurringJournalEntryLine> Lines { get; set; } = [];
}

public sealed class RecurringJournalEntryLine : TenantEntity
{
    public Guid RecurringJournalEntryId { get; set; }
    public RecurringJournalEntry RecurringJournalEntry { get; set; } = default!;
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? Description { get; set; }
}

public sealed class GeneratedRecurringEntry : TenantEntity
{
    public Guid RecurringJournalEntryId { get; set; }
    public Guid JournalEntryId { get; set; }
    public DateOnly GeneratedDate { get; set; }
    public Guid AccountingPeriodId { get; set; }
}

public sealed class ClosingCheck : TenantEntity
{
    public Guid AccountingPeriodId { get; set; }
    public string CheckKey { get; set; } = string.Empty;
    public string CheckNameAr { get; set; } = string.Empty;
    public string CheckNameEn { get; set; } = string.Empty;
    public ClosingCheckStatus Status { get; set; }
    public string MessageAr { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
}

public sealed class ReportDefinition : TenantEntity
{
    public string ReportName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string ColumnsJson { get; set; } = "[]";
    public string FiltersJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
}

public abstract class BusinessParty : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class Customer : BusinessParty;
public sealed class Vendor : BusinessParty;
public sealed class Employee : BusinessParty
{
    public Guid? DepartmentId { get; set; }
    public Guid? BranchId { get; set; }
}
