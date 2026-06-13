using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.Interfaces;

public sealed record ExcelReadResult(IReadOnlyList<string> Headers, IReadOnlyList<ExcelRowData> Rows);
public sealed record ExcelRowData(int RowNumber, IReadOnlyDictionary<string, string?> Values);

public interface IExcelReaderService
{
    Task<ExcelReadResult> ReadWorksheetAsync(string filePath, string? worksheetName, int maxRows, int maxColumns, CancellationToken cancellationToken);
}

public interface IImportService
{
    Task<BaseResponseDto<ImportPreviewDto>> UploadAndValidateAsync(UploadImportRequest request, string originalFileName, string contentType, long sizeInBytes, Stream content, CancellationToken cancellationToken);
    Task<BaseResponseDto<PaginatedResult<ImportBatchSummaryDto>>> GetBatchesAsync(ImportBatchQuery query, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchDetailsDto>> GetBatchDetailsAsync(Guid id, ImportBatchRowsQuery query, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchSummaryDto>> ConfirmImportAsync(Guid id, ConfirmImportRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchSummaryDto>> CancelImportAsync(Guid id, CancelImportRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportTemplateDto>> GenerateTemplateAsync(ImportType importType, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchSummaryDto>> SubmitForReviewAsync(Guid id, SubmitWorkflowRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchSummaryDto>> StartReviewAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchSummaryDto>> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchSummaryDto>> RejectAsync(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken);
    Task<BaseResponseDto<ImportBatchSummaryDto>> ReturnForCorrectionAsync(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken);
}

public sealed record ImportValidationContext(Guid BatchId, ImportType ImportType, Guid? FinancialYearId, Guid? AccountingPeriodId);
public sealed record ImportRowValidationResult(int RowNumber, IReadOnlyDictionary<string, string?> RawData, IReadOnlyDictionary<string, string?>? NormalizedData, ImportRowStatus Status, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
public sealed record ImportConfirmResult(int ImportedRows);

public interface IImportHandler
{
    ImportType SupportedType { get; }
    IReadOnlyList<string> TemplateHeaders { get; }
    Task<IReadOnlyList<ImportRowValidationResult>> ValidateRowsAsync(ImportValidationContext context, IReadOnlyList<ExcelRowData> rows, CancellationToken cancellationToken);
    Task<ImportConfirmResult> ConfirmImportAsync(Guid batchId, CancellationToken cancellationToken);
    ImportTemplateDto GenerateTemplate();
}

public interface IImportHandlerFactory
{
    IImportHandler GetHandler(ImportType importType);
}
