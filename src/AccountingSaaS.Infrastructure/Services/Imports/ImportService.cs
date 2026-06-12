using System.Text.Json;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ImportService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IExcelReaderService excelReader,
    IImportHandlerFactory handlerFactory,
    IAuditLogService auditLog,
    IConfiguration configuration) : IImportService
{
    private static readonly HashSet<string> AllowedMimeTypes =
    [
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/octet-stream"
    ];

    public async Task<BaseResponseDto<ImportPreviewDto>> UploadAndValidateAsync(UploadImportRequest request, string originalFileName, string contentType, long sizeInBytes, Stream content, CancellationToken cancellationToken)
    {
        var tenantId = currentTenant.TenantId;
        if (tenantId is null) return BaseResponseDto<ImportPreviewDto>.Fail("Tenant context is required.");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var maxBytes = configuration.GetValue<long>("Imports:MaxUploadBytes", 10 * 1024 * 1024);
        var maxRows = configuration.GetValue<int>("Imports:MaxRows", 5000);
        var maxColumns = configuration.GetValue<int>("Imports:MaxColumns", 80);
        if (extension != ".xlsx") return BaseResponseDto<ImportPreviewDto>.Fail("Only .xlsx files are allowed.");
        if (!AllowedMimeTypes.Contains(contentType)) return BaseResponseDto<ImportPreviewDto>.Fail("Excel MIME type is not allowed.");
        if (sizeInBytes <= 0 || sizeInBytes > maxBytes) return BaseResponseDto<ImportPreviewDto>.Fail("File size is not allowed.");

        var root = configuration["Imports:StorageRoot"] ?? Path.Combine(AppContext.BaseDirectory, "imports");
        Directory.CreateDirectory(root);
        var storedName = $"{Guid.NewGuid():N}.xlsx";
        var path = Path.Combine(root, storedName);
        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        var batch = new ImportBatch
        {
            ImportType = request.ImportType,
            Status = ImportBatchStatus.Validating,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = storedName,
            FilePath = path,
            ContentType = contentType,
            FileSizeInBytes = sizeInBytes,
            FinancialYearId = request.FinancialYearId,
            AccountingPeriodId = request.AccountingPeriodId,
            UploadedAt = DateTimeOffset.UtcNow,
            UploadedByUserId = currentUser.UserId ?? Guid.Empty,
            Notes = request.Notes
        };
        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Import batch uploaded", tenantId, currentUser.UserId, nameof(ImportBatch), batch.Id.ToString(), newValues: $"{batch.ImportType}|{batch.OriginalFileName}", cancellationToken: cancellationToken);

        try
        {
            var readerResult = await excelReader.ReadWorksheetAsync(path, request.WorksheetName, maxRows, maxColumns, cancellationToken);
            var handler = handlerFactory.GetHandler(request.ImportType);
            var validation = await handler.ValidateRowsAsync(new ImportValidationContext(batch.Id, request.ImportType, request.FinancialYearId, request.AccountingPeriodId), readerResult.Rows, cancellationToken);
            var persistedRows = validation.Where(x => x.RowNumber > 0).Select(x => new ImportBatchRow
            {
                ImportBatchId = batch.Id,
                RowNumber = x.RowNumber,
                RawJson = JsonSerializer.Serialize(x.RawData),
                NormalizedJson = x.NormalizedData is null ? null : JsonSerializer.Serialize(x.NormalizedData),
                Status = x.Status,
                ErrorMessages = x.Errors.Count == 0 ? null : string.Join(Environment.NewLine, x.Errors),
                WarningMessages = x.Warnings.Count == 0 ? null : string.Join(Environment.NewLine, x.Warnings)
            }).ToList();
            dbContext.ImportBatchRows.AddRange(persistedRows);

            batch.TotalRows = persistedRows.Count;
            batch.ValidRows = persistedRows.Count(x => x.Status == ImportRowStatus.Valid);
            batch.WarningRows = persistedRows.Count(x => x.Status == ImportRowStatus.Warning);
            batch.InvalidRows = persistedRows.Count(x => x.Status == ImportRowStatus.Invalid) + validation.Count(x => x.RowNumber == 0 && x.Status == ImportRowStatus.Invalid);
            batch.Status = batch.InvalidRows > 0 ? ImportBatchStatus.HasErrors : ImportBatchStatus.ReadyToImport;
            batch.ValidatedAt = DateTimeOffset.UtcNow;
            batch.ErrorSummary = batch.InvalidRows > 0 ? string.Join("; ", validation.Where(x => x.Status == ImportRowStatus.Invalid).Take(10).SelectMany(x => x.Errors)) : null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLog.LogAsync("Import batch validated", tenantId, currentUser.UserId, nameof(ImportBatch), batch.Id.ToString(), newValues: $"{batch.ImportType}|Rows={batch.TotalRows}|Valid={batch.ValidRows}|Invalid={batch.InvalidRows}", cancellationToken: cancellationToken);

            var previewRows = await dbContext.ImportBatchRows.AsNoTracking().Where(x => x.ImportBatchId == batch.Id).OrderBy(x => x.RowNumber).Take(50).ToListAsync(cancellationToken);
            return BaseResponseDto<ImportPreviewDto>.Ok(new ImportPreviewDto(batch.Id, batch.ImportType, batch.Status, batch.TotalRows, batch.ValidRows, batch.InvalidRows, batch.WarningRows, previewRows.Select(ToRowDto).ToList()), "Import file validated.");
        }
        catch (Exception ex)
        {
            batch.Status = ImportBatchStatus.Failed;
            batch.ErrorSummary = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLog.LogAsync("Import failed", tenantId, currentUser.UserId, nameof(ImportBatch), batch.Id.ToString(), newValues: ex.Message, cancellationToken: cancellationToken);
            return BaseResponseDto<ImportPreviewDto>.Fail("Import validation failed.", [ex.Message]);
        }
    }

    public async Task<BaseResponseDto<PaginatedResult<ImportBatchSummaryDto>>> GetBatchesAsync(ImportBatchQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.PageNumber, 1);
        var size = Math.Clamp(query.PageSize, 1, 200);
        var dbQuery = dbContext.ImportBatches.AsNoTracking();
        if (query.ImportType.HasValue) dbQuery = dbQuery.Where(x => x.ImportType == query.ImportType);
        if (query.Status.HasValue) dbQuery = dbQuery.Where(x => x.Status == query.Status);
        var total = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery.OrderByDescending(x => x.UploadedAt).Skip((page - 1) * size).Take(size).Select(x => ToSummaryDto(x)).ToListAsync(cancellationToken);
        return BaseResponseDto<PaginatedResult<ImportBatchSummaryDto>>.Ok(new PaginatedResult<ImportBatchSummaryDto> { Items = items, TotalCount = total, PageNumber = page, PageSize = size });
    }

    public async Task<BaseResponseDto<ImportBatchDetailsDto>> GetBatchDetailsAsync(Guid id, ImportBatchRowsQuery query, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (batch is null) return BaseResponseDto<ImportBatchDetailsDto>.Fail("Import batch was not found.");
        var page = Math.Max(query.PageNumber, 1);
        var size = Math.Clamp(query.PageSize, 1, 200);
        var rowQuery = dbContext.ImportBatchRows.AsNoTracking().Where(x => x.ImportBatchId == id);
        if (query.Status.HasValue) rowQuery = rowQuery.Where(x => x.Status == query.Status);
        var total = await rowQuery.CountAsync(cancellationToken);
        var rows = await rowQuery.OrderBy(x => x.RowNumber).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return BaseResponseDto<ImportBatchDetailsDto>.Ok(new ImportBatchDetailsDto(ToSummaryDto(batch), new PaginatedResult<ImportBatchRowDto> { Items = rows.Select(ToRowDto).ToList(), TotalCount = total, PageNumber = page, PageSize = size }));
    }

    public async Task<BaseResponseDto<ImportBatchSummaryDto>> ConfirmImportAsync(Guid id, ConfirmImportRequest request, CancellationToken cancellationToken)
    {
        var tenantId = currentTenant.TenantId;
        if (tenantId is null) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Tenant context is required.");
        var batch = await dbContext.ImportBatches.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (batch is null) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Import batch was not found.");
        if (batch.Status == ImportBatchStatus.Imported) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Import batch was already imported.");
        if (batch.Status == ImportBatchStatus.Cancelled) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Cancelled import batch cannot be confirmed.");
        if (batch.Status != ImportBatchStatus.ReadyToImport) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Only validated import batches can be confirmed.");
        if (batch.InvalidRows > 0) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Import batch has invalid rows.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await handlerFactory.GetHandler(batch.ImportType).ConfirmImportAsync(batch.Id, cancellationToken);
            batch.Status = ImportBatchStatus.Imported;
            batch.ImportedRows = result.ImportedRows;
            batch.ImportedAt = DateTimeOffset.UtcNow;
            batch.ImportedByUserId = currentUser.UserId;
            batch.Notes = request.Notes ?? batch.Notes;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await auditLog.LogAsync("Import confirmed", tenantId, currentUser.UserId, nameof(ImportBatch), batch.Id.ToString(), newValues: $"{batch.ImportType}|Rows={batch.TotalRows}|Imported={batch.ImportedRows}", cancellationToken: cancellationToken);
            return BaseResponseDto<ImportBatchSummaryDto>.Ok(ToSummaryDto(batch), "Import confirmed.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            batch.Status = ImportBatchStatus.Failed;
            batch.ErrorSummary = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLog.LogAsync("Import failed", tenantId, currentUser.UserId, nameof(ImportBatch), batch.Id.ToString(), newValues: ex.Message, cancellationToken: cancellationToken);
            return BaseResponseDto<ImportBatchSummaryDto>.Fail("Import failed.", [ex.Message]);
        }
    }

    public async Task<BaseResponseDto<ImportBatchSummaryDto>> CancelImportAsync(Guid id, CancelImportRequest request, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (batch is null) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Import batch was not found.");
        if (batch.Status == ImportBatchStatus.Imported) return BaseResponseDto<ImportBatchSummaryDto>.Fail("Imported batch cannot be cancelled.");
        batch.Status = ImportBatchStatus.Cancelled;
        batch.Notes = request.Reason ?? batch.Notes;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Import cancelled", currentTenant.TenantId, currentUser.UserId, nameof(ImportBatch), batch.Id.ToString(), newValues: $"{batch.ImportType}|{request.Reason}", cancellationToken: cancellationToken);
        return BaseResponseDto<ImportBatchSummaryDto>.Ok(ToSummaryDto(batch), "Import cancelled.");
    }

    public async Task<BaseResponseDto<ImportTemplateDto>> GenerateTemplateAsync(ImportType importType, CancellationToken cancellationToken)
    {
        var template = handlerFactory.GetHandler(importType).GenerateTemplate();
        await auditLog.LogAsync("Import template downloaded", currentTenant.TenantId, currentUser.UserId, nameof(ImportBatch), importType.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<ImportTemplateDto>.Ok(template);
    }

    private static ImportBatchSummaryDto ToSummaryDto(ImportBatch x) => new(x.Id, x.ImportType, x.Status, x.FinancialYearId, x.AccountingPeriodId, x.OriginalFileName, x.TotalRows, x.ValidRows, x.InvalidRows, x.WarningRows, x.ImportedRows, x.UploadedAt, x.ValidatedAt, x.ImportedAt, x.ErrorSummary, x.Notes);

    private static ImportBatchRowDto ToRowDto(ImportBatchRow x) => new(
        x.Id,
        x.RowNumber,
        JsonSerializer.Deserialize<Dictionary<string, string?>>(x.RawJson) ?? [],
        x.NormalizedJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string?>>(x.NormalizedJson),
        x.Status,
        SplitMessages(x.ErrorMessages),
        SplitMessages(x.WarningMessages));

    private static IReadOnlyList<string> SplitMessages(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
