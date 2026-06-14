using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ClosingSubmissionService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog,
    IWorkflowAccessService workflowAccess,
    IClosingAssistantService closingAssistant,
    IDynamicWorkflowService dynamicWorkflow)
    : AccountingServiceBase(dbContext, currentTenant), IClosingSubmissionService
{
    public async Task<BaseResponseDto<ClosingSubmissionDto>> GetByPeriodAsync(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var submission = await DbContext.ClosingSubmissions
            .FirstOrDefaultAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        if (submission is null)
        {
            return BaseResponseDto<ClosingSubmissionDto>.NotFound("طلب التقفيل غير موجود.");
        }

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto);
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> SubmitClosingAsync(
        Guid accountingPeriodId,
        SubmitClosingRequest request,
        CancellationToken cancellationToken)
    {
        var period = await DbContext.AccountingPeriods
            .FindAsync([accountingPeriodId], cancellationToken);

        if (period is null)
        {
            return BaseResponseDto<ClosingSubmissionDto>.NotFound("الفترة المحاسبية غير موجودة.");
        }

        if (period.Status == AccountingPeriodStatus.Closed)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("لا يمكن إعادة إرسال فترة مغلقة للمراجعة.");
        }

        if (period.Status is AccountingPeriodStatus.SubmittedForReview or AccountingPeriodStatus.UnderReview)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("طلب التقفيل قيد المراجعة بالفعل.");
        }

        var hasInvalidJournalEntries = await DbContext.JournalEntries
            .AnyAsync(
                x => x.AccountingPeriodId == accountingPeriodId &&
                     (x.Status == JournalEntryStatus.Draft || x.TotalDebit != x.TotalCredit),
                cancellationToken);

        if (hasInvalidJournalEntries)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "يجب ترحيل جميع القيود والتأكد من توازنها قبل إرسال التقفيل.");
        }

        var hasClosingTasks = await DbContext.ClosingTasks
            .AnyAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        if (!hasClosingTasks)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "يجب إنشاء مهام قائمة التقفيل قبل الإرسال.");
        }

        var hasPendingRequiredTasks = await DbContext.ClosingTasks
            .AnyAsync(
                x => x.AccountingPeriodId == accountingPeriodId &&
                     x.IsRequired &&
                     x.Status != ClosingTaskStatus.Submitted &&
                     x.Status != ClosingTaskStatus.Approved &&
                     x.Status != ClosingTaskStatus.NotApplicable,
                cancellationToken);

        if (hasPendingRequiredTasks)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "يجب إرسال جميع مهام التقفيل الإلزامية قبل إرسال طلب التقفيل.");
        }

        var submission = await DbContext.ClosingSubmissions
            .FirstOrDefaultAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        if (submission is null)
        {
            submission = new ClosingSubmission
            {
                FinancialYearId = period.FinancialYearId,
                AccountingPeriodId = period.Id
            };

            DbContext.ClosingSubmissions.Add(submission);
        }
        else if (submission.Status is not (
                     ClosingSubmissionStatus.Draft or
                     ClosingSubmissionStatus.Rejected or
                     ClosingSubmissionStatus.Reopened or
                     ClosingSubmissionStatus.ReturnedForCorrection))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "لا يمكن إرسال طلب التقفيل من حالته الحالية.");
        }

        var firstStep = await dynamicWorkflow.GetFirstStepAsync(nameof(ClosingSubmission), cancellationToken);
        if (firstStep is null)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("No active period close workflow is configured.");
        }

        submission.Status = ClosingSubmissionStatus.Submitted;
        submission.WorkflowDefinitionId = firstStep.WorkflowDefinitionId;
        submission.WorkflowStepId = firstStep.Id;
        submission.SubmittedAt = DateTimeOffset.UtcNow;
        submission.SubmittedByUserId = currentUser.UserId;
        submission.Notes = request.Notes;
        submission.RejectionReason = null;
        submission.AssignedReviewerUserId = request.ReviewerUserId;

        if (request.ReviewerUserId.HasValue)
        {
            var reviewerAssigned = await DbContext.ReviewerTenantAssignments.AnyAsync(
                x => x.ReviewerUserId == request.ReviewerUserId.Value &&
                     x.TenantId == TenantId &&
                     x.IsActive,
                cancellationToken);
            if (!reviewerAssigned)
            {
                return BaseResponseDto<ClosingSubmissionDto>.Fail("المراجع المحدد غير مسند لهذه الشركة.");
            }
        }

        period.Status = AccountingPeriodStatus.SubmittedForReview;
        period.SubmittedAt = DateTimeOffset.UtcNow;
        period.SubmittedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Submitted",
            TenantId,
            currentUser.UserId,
            nameof(ClosingSubmission),
            submission.Id.ToString(),
            cancellationToken: cancellationToken);
        await dynamicWorkflow.RecordActionAsync(
            nameof(ClosingSubmission), submission.Id, firstStep.WorkflowDefinitionId, firstStep.Id,
            ClosingSubmissionStatus.Draft.ToString(), ClosingSubmissionStatus.Submitted.ToString(),
            WorkflowActionType.Submit, null, request.Notes, cancellationToken);

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, "تم إرسال طلب التقفيل للمراجعة.");
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> StartReviewAsync(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await SetSubmissionStatusAsync(
            accountingPeriodId,
            ClosingSubmissionStatus.Submitted,
            ClosingSubmissionStatus.UnderReview,
            AccountingPeriodStatus.UnderReview,
            "ReviewStarted",
            null,
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> ApproveClosingAsync(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var hasUnapprovedRequiredTasks = await DbContext.ClosingTasks
            .AnyAsync(
                x => x.AccountingPeriodId == accountingPeriodId &&
                     x.IsRequired &&
                     x.Status != ClosingTaskStatus.Approved &&
                     x.Status != ClosingTaskStatus.NotApplicable,
                cancellationToken);

        if (hasUnapprovedRequiredTasks)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "يجب اعتماد جميع مهام التقفيل الإلزامية قبل اعتماد الطلب.");
        }

        var result = await SetSubmissionStatusAsync(
            accountingPeriodId,
            ClosingSubmissionStatus.UnderReview,
            ClosingSubmissionStatus.Approved,
            AccountingPeriodStatus.UnderReview,
            "Approved",
            null,
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> RejectClosingAsync(
        Guid accountingPeriodId,
        RejectClosingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("يجب إدخال سبب الرفض.");
        }

        var result = await SetSubmissionStatusAsync(
            accountingPeriodId,
            ClosingSubmissionStatus.UnderReview,
            ClosingSubmissionStatus.Rejected,
            AccountingPeriodStatus.Rejected,
            "Rejected",
            request.Reason.Trim(),
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> ReturnForCorrectionAsync(
        Guid accountingPeriodId,
        ReturnClosingForCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("يجب إدخال سبب إعادة التقفيل للتصحيح.");
        }

        return await SetSubmissionStatusAsync(
            accountingPeriodId,
            ClosingSubmissionStatus.UnderReview,
            ClosingSubmissionStatus.ReturnedForCorrection,
            AccountingPeriodStatus.Rejected,
            "ReturnedForCorrection",
            request.Reason.Trim(),
            cancellationToken);
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> ClosePeriodAsync(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var submission = await DbContext.ClosingSubmissions
            .FirstOrDefaultAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        var period = await DbContext.AccountingPeriods
            .FindAsync([accountingPeriodId], cancellationToken);

        if (submission is null || period is null)
        {
            return BaseResponseDto<ClosingSubmissionDto>.NotFound("طلب التقفيل غير موجود.");
        }

        if (submission.Status != ClosingSubmissionStatus.Approved)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "يجب اعتماد طلب التقفيل قبل إغلاق الفترة.");
        }

        await closingAssistant.RunAsync(accountingPeriodId, cancellationToken);
        if (await closingAssistant.HasBlockingFailuresAsync(accountingPeriodId, cancellationToken))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("Closing assistant found blocking failures.");
        }

        var hasInvalidJournalEntries = await DbContext.JournalEntries
            .AnyAsync(
                x => x.AccountingPeriodId == accountingPeriodId &&
                     (x.Status == JournalEntryStatus.Draft || x.TotalDebit != x.TotalCredit),
                cancellationToken);

        if (hasInvalidJournalEntries)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "توجد قيود مسودة أو غير متوازنة داخل الفترة.");
        }

        var hasUnapprovedRequiredTasks = await DbContext.ClosingTasks
            .AnyAsync(
                x => x.AccountingPeriodId == accountingPeriodId &&
                     x.IsRequired &&
                     x.Status != ClosingTaskStatus.Approved &&
                     x.Status != ClosingTaskStatus.NotApplicable,
                cancellationToken);

        if (hasUnapprovedRequiredTasks)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "لم يتم اعتماد جميع مهام التقفيل الإلزامية.");
        }

        submission.Status = ClosingSubmissionStatus.Closed;
        submission.ClosedAt = DateTimeOffset.UtcNow;
        submission.ClosedByUserId = currentUser.UserId;

        period.Status = AccountingPeriodStatus.Closed;
        period.ClosedAt = DateTimeOffset.UtcNow;
        period.ClosedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Closed",
            TenantId,
            currentUser.UserId,
            nameof(ClosingSubmission),
            submission.Id.ToString(),
            cancellationToken: cancellationToken);

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, "تم إغلاق الفترة المحاسبية.");
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> ReopenPeriodAsync(
        Guid accountingPeriodId,
        ReopenClosingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("يجب إدخال سبب إعادة فتح الفترة.");
        }

        var submission = await DbContext.ClosingSubmissions
            .FirstOrDefaultAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        var period = await DbContext.AccountingPeriods
            .FindAsync([accountingPeriodId], cancellationToken);

        if (submission is null || period is null)
        {
            return BaseResponseDto<ClosingSubmissionDto>.NotFound("طلب التقفيل غير موجود.");
        }

        if (submission.Status != ClosingSubmissionStatus.Closed ||
            period.Status != AccountingPeriodStatus.Closed)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "يمكن إعادة فتح الفترات المحاسبية المغلقة فقط.");
        }

        submission.Status = ClosingSubmissionStatus.Reopened;
        submission.ReopenedAt = DateTimeOffset.UtcNow;
        submission.ReopenedByUserId = currentUser.UserId;
        submission.ReopenReason = request.Reason.Trim();

        period.Status = AccountingPeriodStatus.Open;
        period.ReopenedAt = DateTimeOffset.UtcNow;
        period.ReopenedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Reopened",
            TenantId,
            currentUser.UserId,
            nameof(ClosingSubmission),
            submission.Id.ToString(),
            newValues: request.Reason,
            cancellationToken: cancellationToken);

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, "تمت إعادة فتح الفترة المحاسبية.");
    }

    private async Task<BaseResponseDto<ClosingSubmissionDto>> SetSubmissionStatusAsync(
        Guid accountingPeriodId,
        ClosingSubmissionStatus requiredStatus,
        ClosingSubmissionStatus submissionStatus,
        AccountingPeriodStatus periodStatus,
        string action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var submission = await DbContext.ClosingSubmissions
            .FirstOrDefaultAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        var period = await DbContext.AccountingPeriods
            .FindAsync([accountingPeriodId], cancellationToken);

        if (submission is null || period is null)
        {
            return BaseResponseDto<ClosingSubmissionDto>.NotFound("طلب التقفيل غير موجود.");
        }

        if (submission.Status != requiredStatus)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "لا يمكن تنفيذ الإجراء من حالة طلب التقفيل الحالية.");
        }

        WorkflowStep? dynamicStep = null;
        if (submission.WorkflowStepId.HasValue)
        {
            dynamicStep = await DbContext.WorkflowSteps.FirstOrDefaultAsync(
                x => x.Id == submission.WorkflowStepId,
                cancellationToken);
        }

        var workflowAction = submissionStatus switch
        {
            ClosingSubmissionStatus.Approved => WorkflowActionType.Approve,
            ClosingSubmissionStatus.Rejected => WorkflowActionType.Reject,
            ClosingSubmissionStatus.ReturnedForCorrection => WorkflowActionType.Return,
            _ => WorkflowActionType.Submit
        };
        if (submissionStatus != ClosingSubmissionStatus.UnderReview &&
            (dynamicStep is null || !await dynamicWorkflow.CanActAsync(dynamicStep, workflowAction, cancellationToken)))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("The current workflow step does not allow this action.");
        }

        if (submissionStatus == ClosingSubmissionStatus.Approved && dynamicStep is { IsFinalApproval: false })
        {
            var nextStep = await dynamicWorkflow.GetNextStepAsync(
                dynamicStep.WorkflowDefinitionId,
                dynamicStep.StepOrder,
                cancellationToken);
            if (nextStep is null)
            {
                return BaseResponseDto<ClosingSubmissionDto>.Fail("Workflow is missing the next approval step.");
            }

            submission.WorkflowStepId = nextStep.Id;
            submissionStatus = ClosingSubmissionStatus.UnderReview;
        }

        if (submissionStatus is ClosingSubmissionStatus.UnderReview
            or ClosingSubmissionStatus.Approved
            or ClosingSubmissionStatus.Rejected
            or ClosingSubmissionStatus.ReturnedForCorrection)
        {
            if (!await workflowAccess.CanReviewTenantAsync(submission.TenantId, cancellationToken))
            {
                return BaseResponseDto<ClosingSubmissionDto>.Fail("ليس لديك صلاحية مراجعة بيانات هذه الشركة.");
            }

            if (submission.AssignedReviewerUserId.HasValue &&
                submission.AssignedReviewerUserId != currentUser.UserId)
            {
                return BaseResponseDto<ClosingSubmissionDto>.Fail("طلب التقفيل مسند إلى مراجع مالي آخر.");
            }

            submission.AssignedReviewerUserId ??= currentUser.UserId;
        }

        submission.Status = submissionStatus;

        if (submissionStatus == ClosingSubmissionStatus.UnderReview)
        {
            submission.ReviewedAt = DateTimeOffset.UtcNow;
            submission.ReviewedByUserId = currentUser.UserId;
        }

        if (submissionStatus == ClosingSubmissionStatus.Approved)
        {
            submission.ApprovedAt = DateTimeOffset.UtcNow;
            submission.ApprovedByUserId = currentUser.UserId;
        }

        if (submissionStatus == ClosingSubmissionStatus.Rejected)
        {
            submission.RejectedAt = DateTimeOffset.UtcNow;
            submission.RejectedByUserId = currentUser.UserId;
            submission.RejectionReason = reason;
        }

        if (submissionStatus == ClosingSubmissionStatus.ReturnedForCorrection)
        {
            submission.RejectedAt = DateTimeOffset.UtcNow;
            submission.RejectedByUserId = currentUser.UserId;
            submission.RejectionReason = reason;
        }

        period.Status = periodStatus;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            action,
            TenantId,
            currentUser.UserId,
            nameof(ClosingSubmission),
            submission.Id.ToString(),
            newValues: reason,
            cancellationToken: cancellationToken);
        if (dynamicStep is not null && submission.WorkflowDefinitionId.HasValue &&
            action != "ReviewStarted")
        {
            await dynamicWorkflow.RecordActionAsync(
                nameof(ClosingSubmission), submission.Id, submission.WorkflowDefinitionId.Value, dynamicStep.Id,
                requiredStatus.ToString(), submissionStatus.ToString(), workflowAction, reason, null, cancellationToken);
        }

        var dto = AccountingMapper.ToDto(submission);

        var message = submissionStatus switch
        {
            ClosingSubmissionStatus.UnderReview => "تم بدء مراجعة طلب التقفيل.",
            ClosingSubmissionStatus.Approved => "تم اعتماد طلب التقفيل.",
            ClosingSubmissionStatus.Rejected => "تم رفض طلب التقفيل وإعادته للمحاسب.",
            ClosingSubmissionStatus.ReturnedForCorrection => "تمت إعادة طلب التقفيل للتصحيح.",
            _ => "تم تحديث حالة طلب التقفيل."
        };

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, message);
    }
}
