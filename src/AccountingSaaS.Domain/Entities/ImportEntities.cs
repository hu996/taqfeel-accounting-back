using AccountingSaaS.Domain.Enums;

namespace AccountingSaaS.Domain.Entities;

public sealed class ImportBatch : TenantEntity
{
    public long ImportBatchNo { get; set; }
    public ImportType ImportType { get; set; }
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Uploaded;
    public WorkflowStatus WorkflowStatus { get; set; } = WorkflowStatus.Draft;
    public Guid? AssignedReviewerUserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public Guid? FinancialYearId { get; set; }
    public Guid? AccountingPeriodId { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int WarningRows { get; set; }
    public int ImportedRows { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public Guid? ImportedByUserId { get; set; }
    public string? ErrorSummary { get; set; }
    public string? Notes { get; set; }
    public ICollection<ImportBatchRow> Rows { get; set; } = [];
}

public sealed class ImportBatchRow : TenantEntity
{
    public Guid ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = default!;
    public int RowNumber { get; set; }
    public string RawJson { get; set; } = "{}";
    public string? NormalizedJson { get; set; }
    public ImportRowStatus Status { get; set; }
    public string? ErrorMessages { get; set; }
    public string? WarningMessages { get; set; }
    public string? ImportedEntityName { get; set; }
    public string? ImportedEntityId { get; set; }
}
