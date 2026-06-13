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

public sealed class DocumentService(AppDbContext dbContext, ICurrentTenantService currentTenant, ICurrentUserService currentUser, IAuditLogService auditLog, IConfiguration configuration, INumberSequenceService numberSequence, IWorkflowAccessService workflowAccess)
    : AccountingServiceBase(dbContext, currentTenant), IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx"];
    private static readonly HashSet<string> AllowedMimeTypes = ["application/pdf", "image/jpeg", "image/png", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"];

    public async Task<BaseResponseDto<DocumentDto>> UploadAsync(UploadDocumentRequest request, string originalFileName, string contentType, long sizeInBytes, Stream content, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var maxBytes = configuration.GetValue<long>("Documents:MaxUploadBytes", 10 * 1024 * 1024);
        if (!AllowedExtensions.Contains(extension)) return BaseResponseDto<DocumentDto>.Fail("امتداد الملف غير مسموح.");
        if (!AllowedMimeTypes.Contains(contentType)) return BaseResponseDto<DocumentDto>.Fail("نوع محتوى الملف غير مسموح.");
        if (sizeInBytes <= 0 || sizeInBytes > maxBytes) return BaseResponseDto<DocumentDto>.Fail("حجم الملف غير مسموح.");
        if (request.AccountingPeriodId.HasValue && !await DbContext.AccountingPeriods.AnyAsync(x => x.Id == request.AccountingPeriodId, cancellationToken)) return BaseResponseDto<DocumentDto>.NotFound("الفترة المحاسبية غير موجودة.");
        if (request.AccountingPeriodId.HasValue && await PeriodIsClosedAsync(request.AccountingPeriodId.Value, cancellationToken)) return BaseResponseDto<DocumentDto>.Fail("لا يمكن رفع مستندات إلى فترة مغلقة.");

        var root = configuration["Documents:StorageRoot"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(root);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(root, storedName);
        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        var documentNo = await numberSequence.NextAsync("DocumentNo", TenantId, cancellationToken);
        var document = new Document { DocumentNo = documentNo, FinancialYearId = request.FinancialYearId, AccountingPeriodId = request.AccountingPeriodId, DocumentType = request.DocumentType, OriginalFileName = Path.GetFileName(originalFileName), StoredFileName = storedName, FilePath = path, ContentType = contentType, SizeInBytes = sizeInBytes, RelatedEntityName = request.RelatedEntityName, RelatedEntityId = request.RelatedEntityId, UploadedAt = DateTimeOffset.UtcNow, UploadedByUserId = currentUser.UserId ?? Guid.Empty, Notes = request.Notes };
        DbContext.Documents.Add(document);
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Document uploaded", TenantId, currentUser.UserId, nameof(Document), document.Id.ToString(), newValues: document.OriginalFileName, cancellationToken: cancellationToken);
        return BaseResponseDto<DocumentDto>.Ok(AccountingMapper.ToDto(document), "تم رفع المستند بنجاح.");
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
        if (document is null) return BaseResponseDto<DocumentDownloadDto>.NotFound("المستند غير موجود.");
        await auditLog.LogAsync("Document downloaded", TenantId, currentUser.UserId, nameof(Document), id.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<DocumentDownloadDto>.Ok(new DocumentDownloadDto(document.FilePath, document.OriginalFileName, document.ContentType));
    }

    public async Task<BaseResponseDto<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await DbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null) return BaseResponseDto<object>.NotFound("المستند غير موجود.");
        if (!workflowAccess.CanEdit(document.WorkflowStatus)
            && !currentUser.Permissions.Contains("Documents.DeleteClosedPeriod", StringComparer.OrdinalIgnoreCase))
            return BaseResponseDto<object>.Fail("لا يمكن حذف المستند في حالة سير العمل الحالية.");
        if (document.AccountingPeriodId.HasValue
            && await DbContext.AccountingPeriods.AnyAsync(x => x.Id == document.AccountingPeriodId && x.Status == AccountingPeriodStatus.Closed, cancellationToken)
            && !currentUser.Permissions.Contains("Documents.DeleteClosedPeriod", StringComparer.OrdinalIgnoreCase))
            return BaseResponseDto<object>.Fail("لا يمكن حذف مستند مرتبط بفترة مغلقة.");
        document.IsDeleted = true;
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Document deleted", TenantId, currentUser.UserId, nameof(Document), id.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<object>.Ok(null, "تم حذف المستند.");
    }

    public async Task<BaseResponseDto<DocumentDto>> SubmitForReviewAsync(
        Guid id,
        SubmitWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        var document = await DbContext.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null) return BaseResponseDto<DocumentDto>.NotFound("المستند غير موجود.");
        if (!workflowAccess.CanEdit(document.WorkflowStatus))
            return BaseResponseDto<DocumentDto>.Fail("لا يمكن إرسال المستند للمراجعة من حالته الحالية.");
        if (request.ReviewerUserId.HasValue && !await DbContext.ReviewerTenantAssignments.AnyAsync(
                x => x.ReviewerUserId == request.ReviewerUserId && x.TenantId == document.TenantId && x.IsActive,
                cancellationToken))
            return BaseResponseDto<DocumentDto>.Fail("المراجع المحدد غير مسند لهذه الشركة.");

        document.WorkflowStatus = WorkflowStatus.Submitted;
        document.AssignedReviewerUserId = request.ReviewerUserId;
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Submitted", document.TenantId, currentUser.UserId, nameof(Document), document.Id.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<DocumentDto>.Ok(AccountingMapper.ToDto(document), "تم إرسال المستند للمراجعة.");
    }

    public Task<BaseResponseDto<DocumentDto>> StartReviewAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeWorkflowAsync(id, WorkflowStatus.Submitted, WorkflowStatus.UnderReview, "ReviewStarted", null, cancellationToken);

    public Task<BaseResponseDto<DocumentDto>> ApproveAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeWorkflowAsync(id, WorkflowStatus.UnderReview, WorkflowStatus.Approved, "Approved", null, cancellationToken);

    public Task<BaseResponseDto<DocumentDto>> RejectAsync(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        ChangeWorkflowAsync(id, WorkflowStatus.UnderReview, WorkflowStatus.Rejected, "Rejected", request.Reason, cancellationToken);

    public Task<BaseResponseDto<DocumentDto>> ReturnForCorrectionAsync(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        ChangeWorkflowAsync(id, WorkflowStatus.UnderReview, WorkflowStatus.ReturnedForCorrection, "ReturnedForCorrection", request.Reason, cancellationToken);

    private async Task<BaseResponseDto<DocumentDto>> ChangeWorkflowAsync(
        Guid id,
        WorkflowStatus required,
        WorkflowStatus target,
        string action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var document = await DbContext.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null) return BaseResponseDto<DocumentDto>.NotFound("المستند غير موجود.");
        if (!await workflowAccess.CanReviewTenantAsync(document.TenantId, cancellationToken))
            return BaseResponseDto<DocumentDto>.Fail("ليس لديك صلاحية مراجعة مستندات هذه الشركة.");
        if (document.AssignedReviewerUserId.HasValue && document.AssignedReviewerUserId != currentUser.UserId)
            return BaseResponseDto<DocumentDto>.Fail("المستند مسند إلى مراجع آخر.");
        if (document.WorkflowStatus != required)
            return BaseResponseDto<DocumentDto>.Fail("لا يمكن تنفيذ الإجراء من حالة المستند الحالية.");
        if (target is WorkflowStatus.Rejected or WorkflowStatus.ReturnedForCorrection && string.IsNullOrWhiteSpace(reason))
            return BaseResponseDto<DocumentDto>.Fail("يجب إدخال سبب واضح.");

        document.WorkflowStatus = target;
        document.AssignedReviewerUserId ??= currentUser.UserId;
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(action, document.TenantId, currentUser.UserId, nameof(Document), document.Id.ToString(), newValues: reason, cancellationToken: cancellationToken);
        return BaseResponseDto<DocumentDto>.Ok(AccountingMapper.ToDto(document), target switch
        {
            WorkflowStatus.UnderReview => "تم بدء مراجعة المستند.",
            WorkflowStatus.Approved => "تم اعتماد المستند.",
            WorkflowStatus.Rejected => "تم رفض المستند.",
            _ => "تمت إعادة المستند للتصحيح."
        });
    }
}
