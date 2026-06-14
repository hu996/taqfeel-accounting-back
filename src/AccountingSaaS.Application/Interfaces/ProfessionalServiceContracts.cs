using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.Interfaces;

public interface INotificationService
{
    Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken);
    Task CreateForRoleAsync(string role, string titleAr, string titleEn, string messageAr, string messageEn, string? entityType, Guid? entityId, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> MarkAsReadAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<NotificationDto>>> GetMyNotificationsAsync(PaginationRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<int>> GetUnreadCountAsync(CancellationToken cancellationToken);
}

public interface IActivityService
{
    Task CreateAsync(ActivityRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<ActivityLogDto>>> GetLatestAsync(int take, CancellationToken cancellationToken);
}

public interface IDynamicWorkflowService
{
    Task<BaseResponseDto<WorkflowDefinitionDto>> SaveDefinitionAsync(Guid? id, SaveWorkflowDefinitionRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<WorkflowDefinitionDto>>> GetDefinitionsAsync(CancellationToken cancellationToken);
    Task<WorkflowStep?> GetFirstStepAsync(string entityType, CancellationToken cancellationToken);
    Task<WorkflowStep?> GetNextStepAsync(Guid definitionId, int currentOrder, CancellationToken cancellationToken);
    Task<bool> CanActAsync(WorkflowStep step, WorkflowActionType action, CancellationToken cancellationToken);
    Task RecordActionAsync(string entityType, Guid entityId, Guid definitionId, Guid stepId, string fromStatus, string toStatus, WorkflowActionType action, string? reason, string? notes, CancellationToken cancellationToken);
}

public interface ICommentService
{
    Task<BaseResponseDto<CommentDto>> AddAsync(CreateCommentRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<CommentDto>>> GetAsync(string entityType, Guid entityId, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IUniversalSearchService
{
    Task<BaseResponseDto<PaginatedResult<SearchResultDto>>> SearchAsync(UniversalSearchRequest request, CancellationToken cancellationToken);
}

public interface ICustomFieldService
{
    Task<BaseResponseDto<CustomFieldDefinitionDto>> SaveDefinitionAsync(Guid? id, SaveCustomFieldDefinitionRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<CustomFieldDefinitionDto>>> GetDefinitionsAsync(string entityType, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> SaveValuesAsync(SaveCustomFieldValuesRequest request, CancellationToken cancellationToken);
}

public interface IDocumentNumberService
{
    Task<BaseResponseDto<DocumentNumberTemplateDto>> SaveTemplateAsync(Guid? id, SaveDocumentNumberTemplateRequest request, CancellationToken cancellationToken);
    Task<string> GenerateAsync(string entityType, DateOnly date, string? branch, CancellationToken cancellationToken);
}

public interface IOpeningBalanceService
{
    Task<BaseResponseDto<OpeningBalanceBatchDto>> CreateAsync(CreateOpeningBalanceBatchRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<OpeningBalanceBatchDto>> SubmitAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<OpeningBalanceBatchDto>> ApproveAsync(Guid id, CancellationToken cancellationToken);
}

public interface IBankReconciliationService
{
    Task<BaseResponseDto<BankAccountDto>> CreateBankAccountAsync(BankAccountRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> AddStatementAsync(BankStatementRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<BankReconciliationDto>> CreateAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> MatchAsync(Guid reconciliationId, MatchBankStatementRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> UnmatchAsync(Guid reconciliationId, Guid statementId, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<ReconciliationDifferenceDto>>> GetDifferencesAsync(Guid reconciliationId, CancellationToken cancellationToken);
}

public interface IFixedAssetService
{
    Task<BaseResponseDto<FixedAssetDto>> CreateAsync(FixedAssetRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<DepreciationRunDto>> RunAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
    Task<BaseResponseDto<DepreciationRunDto>> ApproveAsync(Guid runId, CancellationToken cancellationToken);
}

public interface IRecurringJournalService
{
    Task<BaseResponseDto<RecurringJournalDto>> CreateAsync(CreateRecurringJournalRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<int>> GenerateDueAsync(DateOnly runDate, CancellationToken cancellationToken);
}

public interface IClosingAssistantService
{
    Task<BaseResponseDto<IReadOnlyList<ClosingCheckDto>>> RunAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
    Task<bool> HasBlockingFailuresAsync(Guid accountingPeriodId, CancellationToken cancellationToken);
}

public interface IDashboardService { Task<BaseResponseDto<DashboardDto>> GetMyDashboardAsync(CancellationToken cancellationToken); }

public interface IReportBuilderService
{
    Task<BaseResponseDto<ReportDefinitionDto>> SaveAsync(Guid? id, SaveReportDefinitionRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<IReadOnlyList<ReportDefinitionDto>>> GetAsync(CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<ReportRunResult>> RunAsync(Guid id, RunReportRequest request, CancellationToken cancellationToken);
}

public interface IBusinessPartyService
{
    Task<BaseResponseDto<BusinessPartyDto>> SaveAsync(string type, Guid? id, SaveBusinessPartyRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> DeleteAsync(string type, Guid id, CancellationToken cancellationToken);
}
