using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.DTOs;

public sealed record UploadImportRequest(ImportType ImportType, Guid? FinancialYearId, Guid? AccountingPeriodId, string? WorksheetName, string? Notes);
public sealed record ConfirmImportRequest(string? Notes);
public sealed record CancelImportRequest(string? Reason);

public sealed record ImportBatchSummaryDto(
    Guid Id,
    ImportType ImportType,
    ImportBatchStatus Status,
    Guid? FinancialYearId,
    Guid? AccountingPeriodId,
    string OriginalFileName,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int WarningRows,
    int ImportedRows,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ValidatedAt,
    DateTimeOffset? ImportedAt,
    string? ErrorSummary,
    string? Notes);

public sealed record ImportBatchRowDto(
    Guid Id,
    int RowNumber,
    IReadOnlyDictionary<string, string?> RawData,
    IReadOnlyDictionary<string, string?>? NormalizedData,
    ImportRowStatus Status,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record ImportPreviewDto(
    Guid BatchId,
    ImportType ImportType,
    ImportBatchStatus Status,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int WarningRows,
    IReadOnlyList<ImportBatchRowDto> Rows);

public sealed record ImportBatchDetailsDto(ImportBatchSummaryDto Summary, PaginatedResult<ImportBatchRowDto> Rows);
public sealed record ImportTemplateDto(string FileName, string ContentType, byte[] Content);

public sealed class ImportBatchQuery : PaginationRequest
{
    public ImportType? ImportType { get; init; }
    public ImportBatchStatus? Status { get; init; }
}

public sealed class ImportBatchRowsQuery : PaginationRequest
{
    public ImportRowStatus? Status { get; init; }
}
