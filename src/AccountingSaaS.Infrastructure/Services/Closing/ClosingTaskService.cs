using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ClosingTaskService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog,
    IWorkflowAccessService workflowAccess)
    : AccountingServiceBase(dbContext, currentTenant), IClosingTaskService
{
    public async Task<BaseResponseDto<IReadOnlyList<ClosingTaskDto>>> GetTasksByPeriodAsync(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var tasks = await DbContext.ClosingTasks
            .Where(x => x.AccountingPeriodId == accountingPeriodId)
            .OrderBy(x => x.SortOrder)
            .Select(x => AccountingMapper.ToDto(x))
            .ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<ClosingTaskDto>>.Ok(tasks);
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> AssignTaskAsync(
        Guid id,
        AssignClosingTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [
                ClosingTaskStatus.Pending,
                ClosingTaskStatus.InProgress,
                ClosingTaskStatus.Rejected
            ],
            task =>
            {
                task.AssignedToUserId = request.AssignedToUserId;
                task.DueDate = request.DueDate;
            },
            "Updated",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> StartTaskAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [
                ClosingTaskStatus.Pending,
                ClosingTaskStatus.Rejected
            ],
            task =>
            {
                task.Status = ClosingTaskStatus.InProgress;
                task.RejectionReason = null;
            },
            "Updated",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> SubmitTaskAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [ClosingTaskStatus.InProgress],
            task =>
            {
                task.Status = ClosingTaskStatus.Submitted;
                task.SubmittedAt = DateTimeOffset.UtcNow;
                task.SubmittedByUserId = currentUser.UserId;
            },
            "Submitted",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> ApproveTaskAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [ClosingTaskStatus.Submitted],
            task =>
            {
                task.Status = ClosingTaskStatus.Approved;
                task.ApprovedAt = DateTimeOffset.UtcNow;
                task.ApprovedByUserId = currentUser.UserId;
            },
            "Approved",
            cancellationToken,
            requiresReviewerAccess: true);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> MarkNotApplicableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [
                ClosingTaskStatus.Pending,
                ClosingTaskStatus.InProgress,
                ClosingTaskStatus.Submitted
            ],
            task =>
            {
                task.Status = ClosingTaskStatus.NotApplicable;
            },
            "Updated",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> RejectTaskAsync(
        Guid id,
        RejectClosingTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BaseResponseDto<ClosingTaskDto>.Fail("يجب إدخال سبب الرفض.");
        }

        var result = await UpdateTaskAsync(
            id,
            [ClosingTaskStatus.Submitted],
            task =>
            {
                task.Status = ClosingTaskStatus.Rejected;
                task.RejectedAt = DateTimeOffset.UtcNow;
                task.RejectedByUserId = currentUser.UserId;
                task.RejectionReason = request.Reason.Trim();
            },
            "Rejected",
            cancellationToken,
            requiresReviewerAccess: true);

        return result;
    }

    private async Task<BaseResponseDto<ClosingTaskDto>> UpdateTaskAsync(
        Guid id,
        IReadOnlyCollection<ClosingTaskStatus> allowedStatuses,
        Action<ClosingTask> update,
        string action,
        CancellationToken cancellationToken,
        bool requiresReviewerAccess = false)
    {
        var task = await DbContext.ClosingTasks
            .FindAsync([id], cancellationToken);

        if (task is null)
        {
            return BaseResponseDto<ClosingTaskDto>.NotFound("مهمة التقفيل غير موجودة.");
        }

        var isPeriodClosed = await PeriodIsClosedAsync(
            task.AccountingPeriodId,
            cancellationToken);

        if (isPeriodClosed)
        {
            return BaseResponseDto<ClosingTaskDto>.Fail(
                "لا يمكن تعديل مهام التقفيل داخل فترة مغلقة.");
        }

        if (requiresReviewerAccess &&
            !await workflowAccess.CanReviewTenantAsync(task.TenantId, cancellationToken))
        {
            return BaseResponseDto<ClosingTaskDto>.Fail("ليس لديك صلاحية مراجعة مهام هذه الشركة.");
        }

        if (!requiresReviewerAccess &&
            task.AssignedToUserId.HasValue &&
            task.AssignedToUserId != currentUser.UserId &&
            !currentUser.IsSuperAdmin &&
            !currentUser.IsAccountingOfficeAdmin)
        {
            return BaseResponseDto<ClosingTaskDto>.Fail("مهمة التقفيل مسندة إلى مستخدم آخر.");
        }

        if (!allowedStatuses.Contains(task.Status))
        {
            return BaseResponseDto<ClosingTaskDto>.Fail(
                "لا يمكن تنفيذ الإجراء من حالة مهمة التقفيل الحالية.");
        }

        update(task);

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            action,
            TenantId,
            currentUser.UserId,
            nameof(ClosingTask),
            id.ToString(),
            cancellationToken: cancellationToken);

        var dto = AccountingMapper.ToDto(task);

        var message = task.Status switch
        {
            ClosingTaskStatus.InProgress => "تم بدء تنفيذ مهمة التقفيل.",
            ClosingTaskStatus.Submitted => "تم إرسال مهمة التقفيل للمراجعة.",
            ClosingTaskStatus.Approved => "تم اعتماد مهمة التقفيل.",
            ClosingTaskStatus.Rejected => "تم رفض مهمة التقفيل وإعادتها للتصحيح.",
            ClosingTaskStatus.NotApplicable => "تم تحديد المهمة كغير منطبقة.",
            _ => "تم تحديث مهمة التقفيل."
        };

        return BaseResponseDto<ClosingTaskDto>.Ok(dto, message);
    }
}
