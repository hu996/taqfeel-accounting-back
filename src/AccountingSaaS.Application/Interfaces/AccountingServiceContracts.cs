using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.Interfaces;

public interface IFinancialYearService
{
    Task<BaseResponseDto<FinancialYearDto>> CreateAsync(CreateFinancialYearRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<FinancialYearDto>> UpdateAsync(Guid id, UpdateFinancialYearRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<FinancialYearDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<FinancialYearDto>>> GetPagedAsync(AccountingPagedRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<FinancialYearDto>> CloseYearAsync(Guid id, CancellationToken cancellationToken);
}

public interface IAccountingPeriodService
{
    Task<BaseResponseDto<AccountingPeriodDto>> CreateAsync(CreateAccountingPeriodRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountingPeriodDto>> UpdateAsync(Guid id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountingPeriodDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<AccountingPeriodDto>>> GetPagedAsync(AccountingPagedRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountingPeriodDto>> LockPeriodAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountingPeriodDto>> SubmitForReviewAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountingPeriodDto>> ClosePeriodAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountingPeriodDto>> ReopenPeriodAsync(Guid id, ReopenPeriodRequest request, CancellationToken cancellationToken);
}

public interface IChartOfAccountsService
{
    Task<BaseResponseDto<AccountDto>> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountDto>> UpdateAccountAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountDto>> GetAccountAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<AccountDto>>> GetTreeAsync(CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<AccountDto>>> GetPagedAsync(AccountingPagedRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountDto>> ActivateAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<AccountDto>> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICostCenterService
{
    Task<BaseResponseDto<CostCenterDto>> CreateAsync(CreateCostCenterRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<CostCenterDto>> UpdateAsync(Guid id, UpdateCostCenterRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<CostCenterDto>>> GetPagedAsync(AccountingPagedRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<CostCenterDto>> ActivateAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<CostCenterDto>> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

public interface IJournalEntryService
{
    Task<BaseResponseDto<JournalEntryDto>> CreateDraftAsync(CreateJournalEntryRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> UpdateDraftAsync(Guid id, UpdateJournalEntryRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<JournalEntryDto>>> GetPagedAsync(AccountingPagedRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> PostAsync(Guid id, PostJournalEntryRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> ReverseAsync(Guid id, ReverseJournalEntryRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> SubmitForReviewAsync(Guid id, SubmitJournalEntryRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> StartReviewAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> RejectAsync(Guid id, ReviewJournalEntryRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<JournalEntryDto>> ReturnForCorrectionAsync(Guid id, ReviewJournalEntryRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<JournalEntryDto>>> GetMyReviewQueueAsync(AccountingPagedRequest request, CancellationToken cancellationToken);
}

public interface IDocumentService
{
    Task<BaseResponseDto<DocumentDto>> UploadAsync(UploadDocumentRequest request, string originalFileName, string contentType, long sizeInBytes, Stream content, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<DocumentDto>>> GetPagedAsync(AccountingPagedRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<DocumentDownloadDto>> DownloadAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<DocumentDto>> SubmitForReviewAsync(Guid id, SubmitWorkflowRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<DocumentDto>> StartReviewAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<DocumentDto>> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<DocumentDto>> RejectAsync(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<DocumentDto>> ReturnForCorrectionAsync(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken);
}

public sealed record DocumentDownloadDto(string FilePath, string OriginalFileName, string ContentType);

public interface IClosingChecklistService
{
    Task<BaseResponseDto<ClosingChecklistTemplateDto>> CreateTemplateAsync(CreateClosingChecklistTemplateRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingChecklistTemplateDto>> CreateDefaultTemplateAsync(CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingChecklistTemplateDto>> UpdateTemplateAsync(Guid id, UpdateClosingChecklistTemplateRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingChecklistTemplateItemDto>> AddTemplateItemAsync(CreateClosingChecklistTemplateItemRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingChecklistTemplateItemDto>> UpdateTemplateItemAsync(Guid id, UpdateClosingChecklistTemplateItemRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<ClosingTaskDto>>> GenerateTasksForPeriodAsync(GenerateClosingTasksRequest request, CancellationToken cancellationToken);
}

public interface IClosingTaskService
{
    Task<BaseResponseDto<IReadOnlyList<ClosingTaskDto>>> GetTasksByPeriodAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingTaskDto>> AssignTaskAsync(Guid id, AssignClosingTaskRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingTaskDto>> StartTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingTaskDto>> SubmitTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingTaskDto>> ApproveTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingTaskDto>> RejectTaskAsync(Guid id, RejectClosingTaskRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingTaskDto>> MarkNotApplicableAsync(Guid id, CancellationToken cancellationToken);
}

public interface IClosingSubmissionService
{
    Task<BaseResponseDto<ClosingSubmissionDto>> GetByPeriodAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingSubmissionDto>> SubmitClosingAsync(Guid accountingPeriodId, SubmitClosingRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingSubmissionDto>> StartReviewAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingSubmissionDto>> ApproveClosingAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingSubmissionDto>> RejectClosingAsync(Guid accountingPeriodId, RejectClosingRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingSubmissionDto>> ReturnForCorrectionAsync(Guid accountingPeriodId, ReturnClosingForCorrectionRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingSubmissionDto>> ClosePeriodAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingSubmissionDto>> ReopenPeriodAsync(Guid accountingPeriodId, ReopenClosingRequest request, CancellationToken cancellationToken);
}

public interface IAccountingReportService
{
    Task<BaseResponseDto<IReadOnlyList<TrialBalanceRowDto>>> GetTrialBalanceAsync(ReportDateRangeRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<LedgerReportDto>> GetGeneralLedgerAsync(AccountReportRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<LedgerReportDto>> GetAccountStatementAsync(AccountReportRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ClosingProgressDto>> GetClosingProgressAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
}
