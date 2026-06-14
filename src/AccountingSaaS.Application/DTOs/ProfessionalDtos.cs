using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.DTOs;

public sealed record NotificationDto(Guid Id, string TitleAr, string TitleEn, string MessageAr, string MessageEn, string? EntityType, Guid? EntityId, bool IsRead, DateTimeOffset CreatedAt);
public sealed record CreateNotificationRequest(Guid UserId, string TitleAr, string TitleEn, string MessageAr, string MessageEn, string? EntityType, Guid? EntityId);
public sealed record ActivityLogDto(Guid Id, Guid UserId, string Action, string EntityType, Guid EntityId, string TitleAr, string TitleEn, string DescriptionAr, string DescriptionEn, DateTimeOffset CreatedAt);
public sealed record ActivityRequest(string Action, string EntityType, Guid EntityId, string TitleAr, string TitleEn, string DescriptionAr, string DescriptionEn);

public sealed record WorkflowStepDto(Guid Id, int StepOrder, string StepNameAr, string StepNameEn, Guid? RequiredRoleId, string? RequiredPermission, bool CanApprove, bool CanReject, bool CanReturn, bool IsFinalApproval);
public sealed record WorkflowDefinitionDto(Guid Id, string NameAr, string NameEn, string EntityType, bool IsActive, IReadOnlyList<WorkflowStepDto> Steps);
public sealed record WorkflowStepRequest(int StepOrder, string StepNameAr, string StepNameEn, Guid? RequiredRoleId, string? RequiredPermission, bool CanApprove, bool CanReject, bool CanReturn, bool IsFinalApproval);
public sealed record SaveWorkflowDefinitionRequest(string NameAr, string NameEn, string EntityType, bool IsActive, IReadOnlyList<WorkflowStepRequest> Steps);
public sealed record WorkflowActionDto(Guid Id, Guid WorkflowStepId, string FromStatus, string ToStatus, WorkflowActionType Action, Guid ActionByUserId, DateTimeOffset ActionDate, string? Reason, string? Notes);

public sealed record CommentDto(Guid Id, string EntityType, Guid EntityId, string CommentText, Guid? CreatedBy, DateTimeOffset CreatedAt, bool IsInternal);
public sealed record CreateCommentRequest(string EntityType, Guid EntityId, string CommentText, bool IsInternal);
public sealed record JournalEntryVersionDto(Guid Id, int VersionNo, string SnapshotJson, Guid? CreatedBy, DateTimeOffset CreatedAt, string? ChangeReason);

public sealed record SearchResultDto(string EntityType, Guid EntityId, string Title, string? Subtitle, string? Code, string? UrlKey);
public sealed class UniversalSearchRequest : PaginationRequest { public string Keyword { get; init; } = string.Empty; }

public sealed record CustomFieldDefinitionDto(Guid Id, string EntityType, string FieldKey, string FieldNameAr, string FieldNameEn, CustomFieldType FieldType, bool IsRequired, string? OptionsJson, bool IsActive);
public sealed record SaveCustomFieldDefinitionRequest(string EntityType, string FieldKey, string FieldNameAr, string FieldNameEn, CustomFieldType FieldType, bool IsRequired, string? OptionsJson, bool IsActive);
public sealed record CustomFieldValueRequest(Guid CustomFieldDefinitionId, string? Value);
public sealed record SaveCustomFieldValuesRequest(string EntityType, Guid EntityId, IReadOnlyList<CustomFieldValueRequest> Values);

public sealed record DocumentNumberTemplateDto(Guid Id, string EntityType, string Template, ResetPeriod ResetPeriod, bool IsActive);
public sealed record SaveDocumentNumberTemplateRequest(string EntityType, string Template, ResetPeriod ResetPeriod, bool IsActive);

public sealed record OpeningBalanceLineRequest(Guid AccountId, decimal Debit, decimal Credit, Guid? CostCenterId, string? Notes);
public sealed record CreateOpeningBalanceBatchRequest(Guid FinancialYearId, IReadOnlyList<OpeningBalanceLineRequest> Lines);
public sealed record OpeningBalanceBatchDto(Guid Id, Guid FinancialYearId, string BatchNo, OpeningBalanceStatus Status, Guid? JournalEntryId, decimal TotalDebit, decimal TotalCredit);

public sealed record BankAccountRequest(Guid AccountId, string BankName, string AccountNumber, string? Iban, bool IsActive);
public sealed record BankAccountDto(Guid Id, Guid AccountId, string BankName, string AccountNumber, string? Iban, bool IsActive);
public sealed record BankStatementRequest(Guid BankAccountId, DateOnly StatementDate, string Description, decimal Debit, decimal Credit, string? ReferenceNo);
public sealed record CreateBankReconciliationRequest(Guid BankAccountId, Guid AccountingPeriodId);
public sealed record MatchBankStatementRequest(Guid BankStatementId, Guid JournalEntryLineId, decimal MatchedAmount);
public sealed record BankReconciliationDto(Guid Id, Guid BankAccountId, Guid AccountingPeriodId, ReconciliationStatus Status);
public sealed record ReconciliationDifferenceDto(Guid StatementId, DateOnly Date, string Description, decimal Amount, string? ReferenceNo);

public sealed record FixedAssetRequest(string AssetCode, string AssetNameAr, string AssetNameEn, DateOnly PurchaseDate, decimal PurchaseCost, int UsefulLifeMonths, decimal SalvageValue, Guid AssetAccountId, Guid DepreciationExpenseAccountId, Guid AccumulatedDepreciationAccountId);
public sealed record FixedAssetDto(Guid Id, string AssetCode, string AssetNameAr, string AssetNameEn, decimal PurchaseCost, decimal AccumulatedDepreciation, decimal BookValue, FixedAssetStatus Status);
public sealed record DepreciationRunDto(Guid Id, Guid AccountingPeriodId, DateOnly RunDate, DepreciationRunStatus Status, decimal TotalDepreciation);

public sealed record RecurringJournalLineRequest(Guid AccountId, decimal Debit, decimal Credit, Guid? CostCenterId, string? Description);
public sealed record CreateRecurringJournalRequest(string Name, RecurringFrequency Frequency, DateOnly StartDate, DateOnly? EndDate, DateOnly NextRunDate, IReadOnlyList<RecurringJournalLineRequest> Lines);
public sealed record RecurringJournalDto(Guid Id, string Name, RecurringFrequency Frequency, DateOnly NextRunDate, bool IsActive);

public sealed record ClosingCheckDto(Guid Id, string CheckKey, string CheckNameAr, string CheckNameEn, ClosingCheckStatus Status, string MessageAr, string MessageEn);

public sealed record DashboardDto(int UnderReview, int Rejected, int ApprovedToday, int ApprovedNotPosted, int OpenPeriods, int MyTasks, int UnreadNotifications, IReadOnlyList<NotificationDto> LatestNotifications, IReadOnlyList<ActivityLogDto> LatestActivities);

public sealed record ReportDefinitionDto(Guid Id, string ReportName, string EntityType, string ColumnsJson, string FiltersJson, bool IsActive);
public sealed record SaveReportDefinitionRequest(string ReportName, string EntityType, string ColumnsJson, string FiltersJson, bool IsActive);
public sealed record RunReportRequest(IReadOnlyDictionary<string, string?> Filters, int PageNumber = 1, int PageSize = 50);
public sealed record ReportRunResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, int TotalCount);

public sealed record BusinessPartyDto(Guid Id, string Code, string NameAr, string NameEn, bool IsActive);
public sealed record SaveBusinessPartyRequest(string Code, string NameAr, string NameEn, bool IsActive = true);
