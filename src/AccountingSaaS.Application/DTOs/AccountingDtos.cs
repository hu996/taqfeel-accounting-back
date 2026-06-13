using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.DTOs;

public sealed record FinancialYearDto(Guid Id, string YearName, DateOnly StartDate, DateOnly EndDate, FinancialYearStatus Status);
public sealed record CreateFinancialYearRequest(string YearName, DateOnly StartDate, DateOnly EndDate);
public sealed record UpdateFinancialYearRequest(string YearName, DateOnly StartDate, DateOnly EndDate);

public sealed record AccountingPeriodDto(Guid Id, Guid FinancialYearId, string PeriodName, DateOnly StartDate, DateOnly EndDate, AccountingPeriodStatus Status);
public sealed record CreateAccountingPeriodRequest(Guid FinancialYearId, string PeriodName, DateOnly StartDate, DateOnly EndDate);
public sealed record UpdateAccountingPeriodRequest(string PeriodName, DateOnly StartDate, DateOnly EndDate);
public sealed record ReopenPeriodRequest(string Reason);

public sealed record AccountDto(Guid Id, string Code, string NameAr, string NameEn, AccountType AccountType, NormalBalance NormalBalance, Guid? ParentAccountId, bool IsPostingAccount, bool IsActive)
{
    public long AccountNo { get; init; }
}
public sealed record CreateAccountRequest(string Code, string NameAr, string NameEn, AccountType AccountType, NormalBalance NormalBalance, Guid? ParentAccountId, bool IsPostingAccount);
public sealed record UpdateAccountRequest(string Code, string NameAr, string NameEn, AccountType AccountType, NormalBalance NormalBalance, Guid? ParentAccountId, bool IsPostingAccount);

public sealed record CostCenterDto(Guid Id, string Code, string Name, bool IsActive)
{
    public long CostCenterNo { get; init; }
}
public sealed record CreateCostCenterRequest(string Code, string Name);
public sealed record UpdateCostCenterRequest(string Code, string Name);

public sealed record JournalEntryLineRequest(Guid AccountId, Guid? CostCenterId, decimal Debit, decimal Credit, string? Description);
public sealed record JournalEntryLineDto(Guid Id, Guid AccountId, string AccountCode, string AccountName, Guid? CostCenterId, decimal Debit, decimal Credit, string? Description);
public sealed record JournalEntryDto(Guid Id, Guid FinancialYearId, Guid AccountingPeriodId, string EntryNumber, DateOnly EntryDate, string Description, JournalEntryStatus Status, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<JournalEntryLineDto> Lines)
{
    public long JournalEntryNo { get; init; }
    public WorkflowStatus WorkflowStatus { get; init; }
    public Guid? AssignedReviewerUserId { get; init; }
}
public sealed record CreateJournalEntryRequest(Guid FinancialYearId, Guid AccountingPeriodId, DateOnly EntryDate, string Description, IReadOnlyList<JournalEntryLineRequest> Lines);
public sealed record UpdateJournalEntryRequest(DateOnly EntryDate, string Description, IReadOnlyList<JournalEntryLineRequest> Lines);
public sealed record PostJournalEntryRequest(string? Notes);
public sealed record ReverseJournalEntryRequest(string Reason);
public sealed record SubmitJournalEntryRequest(Guid? ReviewerUserId);
public sealed record ReviewJournalEntryRequest(string? Reason);
public sealed record SubmitWorkflowRequest(Guid? ReviewerUserId);
public sealed record WorkflowDecisionRequest(string? Reason);

public sealed record DocumentDto(Guid Id, Guid? FinancialYearId, Guid? AccountingPeriodId, DocumentType DocumentType, string OriginalFileName, string ContentType, long SizeInBytes, string? RelatedEntityName, string? RelatedEntityId, DateTimeOffset UploadedAt, string? Notes)
{
    public long DocumentNo { get; init; }
    public WorkflowStatus WorkflowStatus { get; init; }
}
public sealed record UploadDocumentRequest(Guid? FinancialYearId, Guid? AccountingPeriodId, DocumentType DocumentType, string? RelatedEntityName, string? RelatedEntityId, string? Notes);

public sealed record ClosingChecklistTemplateDto(Guid Id, string Name, string? Description, bool IsDefault, bool IsActive, IReadOnlyList<ClosingChecklistTemplateItemDto> Items);
public sealed record ClosingChecklistTemplateItemDto(Guid Id, Guid TemplateId, string Title, string? Description, int SortOrder, bool IsRequired);
public sealed record CreateClosingChecklistTemplateRequest(string Name, string? Description, bool IsDefault);
public sealed record UpdateClosingChecklistTemplateRequest(string Name, string? Description, bool IsDefault, bool IsActive);
public sealed record CreateClosingChecklistTemplateItemRequest(Guid TemplateId, string Title, string? Description, int SortOrder, bool IsRequired);
public sealed record UpdateClosingChecklistTemplateItemRequest(string Title, string? Description, int SortOrder, bool IsRequired);
public sealed record GenerateClosingTasksRequest(Guid FinancialYearId, Guid AccountingPeriodId, Guid TemplateId);

public sealed record ClosingTaskDto(Guid Id, Guid FinancialYearId, Guid AccountingPeriodId, Guid? TemplateItemId, string Title, string? Description, int SortOrder, bool IsRequired, Guid? AssignedToUserId, ClosingTaskStatus Status, DateOnly? DueDate, string? RejectionReason);
public sealed record CreateClosingTaskRequest(Guid FinancialYearId, Guid AccountingPeriodId, string Title, string? Description, int SortOrder, bool IsRequired, DateOnly? DueDate);
public sealed record AssignClosingTaskRequest(Guid? AssignedToUserId, DateOnly? DueDate);
public sealed record RejectClosingTaskRequest(string Reason);

public sealed record ClosingSubmissionDto(Guid Id, Guid FinancialYearId, Guid AccountingPeriodId, ClosingSubmissionStatus Status, string? Notes, string? RejectionReason, string? ReopenReason)
{
    public Guid? AssignedReviewerUserId { get; init; }
}
public sealed record SubmitClosingRequest(string? Notes)
{
    public Guid? ReviewerUserId { get; init; }
}
public sealed record RejectClosingRequest(string Reason);
public sealed record ReturnClosingForCorrectionRequest(string Reason);
public sealed record ReopenClosingRequest(string Reason);

public sealed record TrialBalanceRowDto(string AccountCode, string AccountName, decimal OpeningDebit, decimal OpeningCredit, decimal PeriodDebit, decimal PeriodCredit, decimal ClosingDebit, decimal ClosingCredit);
public sealed record LedgerLineDto(DateOnly EntryDate, string EntryNumber, string Description, decimal Debit, decimal Credit, decimal RunningBalance);
public sealed record LedgerReportDto(string AccountCode, string AccountName, decimal OpeningBalance, IReadOnlyList<LedgerLineDto> Lines);
public sealed record ClosingProgressDto(int TotalTasks, int RequiredTasks, int ApprovedTasks, int PendingTasks, int RejectedTasks, decimal CompletionPercentage, ClosingSubmissionStatus? CurrentSubmissionStatus);
public sealed record ReportDateRangeRequest(Guid? AccountingPeriodId, DateOnly? DateFrom, DateOnly? DateTo);
public sealed record AccountReportRequest(Guid AccountId, DateOnly? DateFrom, DateOnly? DateTo);

public sealed class AccountingPagedRequest : PaginationRequest
{
    public Guid? FinancialYearId { get; init; }
    public Guid? AccountingPeriodId { get; init; }
    public string? Search { get; init; }
    public JournalEntryStatus? Status { get; init; }
    public WorkflowStatus? WorkflowStatus { get; init; }
}
