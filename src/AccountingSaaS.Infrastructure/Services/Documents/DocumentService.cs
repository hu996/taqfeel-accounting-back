using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class DocumentService(AppDbContext dbContext, ICurrentTenantService currentTenant, ICurrentUserService currentUser, IAuditLogService auditLog, IConfiguration configuration)
    : AccountingServiceBase(dbContext, currentTenant), IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx"];
    private static readonly HashSet<string> AllowedMimeTypes = ["application/pdf", "image/jpeg", "image/png", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"];

    public async Task<BaseResponseDto<DocumentDto>> UploadAsync(UploadDocumentRequest request, string originalFileName, string contentType, long sizeInBytes, Stream content, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var maxBytes = configuration.GetValue<long>("Documents:MaxUploadBytes", 10 * 1024 * 1024);
        if (!AllowedExtensions.Contains(extension)) return BaseResponseDto<DocumentDto>.Fail("File extension is not allowed.");
        if (!AllowedMimeTypes.Contains(contentType)) return BaseResponseDto<DocumentDto>.Fail("File content type is not allowed.");
        if (sizeInBytes <= 0 || sizeInBytes > maxBytes) return BaseResponseDto<DocumentDto>.Fail("File size is not allowed.");
        if (request.AccountingPeriodId.HasValue && !await DbContext.AccountingPeriods.AnyAsync(x => x.Id == request.AccountingPeriodId, cancellationToken)) return BaseResponseDto<DocumentDto>.Fail("Accounting period was not found.");
        if (request.AccountingPeriodId.HasValue && await PeriodIsClosedAsync(request.AccountingPeriodId.Value, cancellationToken)) return BaseResponseDto<DocumentDto>.Fail("Documents cannot be uploaded to a closed period.");

        var root = configuration["Documents:StorageRoot"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(root);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(root, storedName);
        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        var document = new Document { FinancialYearId = request.FinancialYearId, AccountingPeriodId = request.AccountingPeriodId, DocumentType = request.DocumentType, OriginalFileName = Path.GetFileName(originalFileName), StoredFileName = storedName, FilePath = path, ContentType = contentType, SizeInBytes = sizeInBytes, RelatedEntityName = request.RelatedEntityName, RelatedEntityId = request.RelatedEntityId, UploadedAt = DateTimeOffset.UtcNow, UploadedByUserId = currentUser.UserId ?? Guid.Empty, Notes = request.Notes };
        DbContext.Documents.Add(document);
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Document uploaded", TenantId, currentUser.UserId, nameof(Document), document.Id.ToString(), newValues: document.OriginalFileName, cancellationToken: cancellationToken);
        return BaseResponseDto<DocumentDto>.Ok(AccountingMapper.ToDto(document), "Document uploaded.");
    }

    public async Task<BaseResponseDto<PaginatedResult<DocumentDto>>> GetPagedAsync(AccountingPagedRequest request, CancellationToken cancellationToken)
    {
        var query = DbContext.Documents.AsQueryable();
        if (request.FinancialYearId.HasValue) query = query.Where(x => x.FinancialYearId == request.FinancialYearId);
        if (request.AccountingPeriodId.HasValue) query = query.Where(x => x.AccountingPeriodId == request.AccountingPeriodId);
        return BaseResponseDto<PaginatedResult<DocumentDto>>.Ok(await ToPagedAsync(query.OrderByDescending(x => x.UploadedAt).Select(x => AccountingMapper.ToDto(x)), request, cancellationToken));
    }

    public async Task<BaseResponseDto<DocumentDownloadDto>> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await DbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null) return BaseResponseDto<DocumentDownloadDto>.Fail("Document was not found.");
        await auditLog.LogAsync("Document downloaded", TenantId, currentUser.UserId, nameof(Document), id.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<DocumentDownloadDto>.Ok(new DocumentDownloadDto(document.FilePath, document.OriginalFileName, document.ContentType));
    }

    public async Task<BaseResponseDto<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await DbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null) return BaseResponseDto<object>.Fail("Document was not found.");
        if (document.AccountingPeriodId.HasValue
            && await DbContext.AccountingPeriods.AnyAsync(x => x.Id == document.AccountingPeriodId && x.Status == AccountingPeriodStatus.Closed, cancellationToken)
            && !currentUser.Permissions.Contains("Documents.DeleteClosedPeriod", StringComparer.OrdinalIgnoreCase))
            return BaseResponseDto<object>.Fail("Documents linked to closed periods cannot be deleted.");
        document.IsDeleted = true;
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Document deleted", TenantId, currentUser.UserId, nameof(Document), id.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<object>.Ok(null, "Document deleted.");
    }
}
