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
    IAuditLogService auditLog)
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
            return BaseResponseDto<ClosingSubmissionDto>.Fail("Closing submission was not found.");
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
            return BaseResponseDto<ClosingSubmissionDto>.Fail("Accounting period was not found.");
        }

        if (period.Status == AccountingPeriodStatus.Closed)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("Closed periods cannot be submitted again.");
        }

        if (period.Status is AccountingPeriodStatus.SubmittedForReview or AccountingPeriodStatus.UnderReview)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("The closing submission is already being reviewed.");
        }

        var hasInvalidJournalEntries = await DbContext.JournalEntries
            .AnyAsync(
                x => x.AccountingPeriodId == accountingPeriodId &&
                     (x.Status == JournalEntryStatus.Draft || x.TotalDebit != x.TotalCredit),
                cancellationToken);

        if (hasInvalidJournalEntries)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "All journal entries must be posted and balanced before submission.");
        }

        var hasClosingTasks = await DbContext.ClosingTasks
            .AnyAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        if (!hasClosingTasks)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "Generate the closing checklist tasks before submission.");
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
                "All required closing tasks must be submitted before the closing submission.");
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
                     ClosingSubmissionStatus.Reopened))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "The closing submission cannot be submitted from its current status.");
        }

        submission.Status = ClosingSubmissionStatus.Submitted;
        submission.SubmittedAt = DateTimeOffset.UtcNow;
        submission.SubmittedByUserId = currentUser.UserId;
        submission.Notes = request.Notes;
        submission.RejectionReason = null;

        period.Status = AccountingPeriodStatus.SubmittedForReview;
        period.SubmittedAt = DateTimeOffset.UtcNow;
        period.SubmittedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Closing submission submitted",
            TenantId,
            currentUser.UserId,
            nameof(ClosingSubmission),
            submission.Id.ToString(),
            cancellationToken: cancellationToken);

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, "Closing submitted.");
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
            "Closing submission under review",
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
                "All required closing tasks must be approved before approving the submission.");
        }

        var result = await SetSubmissionStatusAsync(
            accountingPeriodId,
            ClosingSubmissionStatus.UnderReview,
            ClosingSubmissionStatus.Approved,
            AccountingPeriodStatus.UnderReview,
            "Closing submission approved",
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
            return BaseResponseDto<ClosingSubmissionDto>.Fail("A rejection reason is required.");
        }

        var result = await SetSubmissionStatusAsync(
            accountingPeriodId,
            ClosingSubmissionStatus.UnderReview,
            ClosingSubmissionStatus.Rejected,
            AccountingPeriodStatus.Rejected,
            "Closing submission rejected",
            request.Reason.Trim(),
            cancellationToken);

        return result;
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
            return BaseResponseDto<ClosingSubmissionDto>.Fail("Closing submission was not found.");
        }

        if (submission.Status != ClosingSubmissionStatus.Approved)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "Closing submission must be approved before closing.");
        }

        var hasInvalidJournalEntries = await DbContext.JournalEntries
            .AnyAsync(
                x => x.AccountingPeriodId == accountingPeriodId &&
                     (x.Status == JournalEntryStatus.Draft || x.TotalDebit != x.TotalCredit),
                cancellationToken);

        if (hasInvalidJournalEntries)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "Period has draft or unbalanced journal entries.");
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
                "Required closing tasks are not approved.");
        }

        submission.Status = ClosingSubmissionStatus.Closed;
        submission.ClosedAt = DateTimeOffset.UtcNow;
        submission.ClosedByUserId = currentUser.UserId;

        period.Status = AccountingPeriodStatus.Closed;
        period.ClosedAt = DateTimeOffset.UtcNow;
        period.ClosedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Closing submission closed",
            TenantId,
            currentUser.UserId,
            nameof(ClosingSubmission),
            submission.Id.ToString(),
            cancellationToken: cancellationToken);

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, "Period closed.");
    }

    public async Task<BaseResponseDto<ClosingSubmissionDto>> ReopenPeriodAsync(
        Guid accountingPeriodId,
        ReopenClosingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("A reopen reason is required.");
        }

        var submission = await DbContext.ClosingSubmissions
            .FirstOrDefaultAsync(x => x.AccountingPeriodId == accountingPeriodId, cancellationToken);

        var period = await DbContext.AccountingPeriods
            .FindAsync([accountingPeriodId], cancellationToken);

        if (submission is null || period is null)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail("Closing submission was not found.");
        }

        if (submission.Status != ClosingSubmissionStatus.Closed ||
            period.Status != AccountingPeriodStatus.Closed)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                "Only a closed accounting period can be reopened.");
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
            "Closing submission reopened",
            TenantId,
            currentUser.UserId,
            nameof(ClosingSubmission),
            submission.Id.ToString(),
            newValues: request.Reason,
            cancellationToken: cancellationToken);

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, "Period reopened.");
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
            return BaseResponseDto<ClosingSubmissionDto>.Fail("Closing submission was not found.");
        }

        if (submission.Status != requiredStatus)
        {
            return BaseResponseDto<ClosingSubmissionDto>.Fail(
                $"Closing submission must be {requiredStatus} before this action.");
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

        var dto = AccountingMapper.ToDto(submission);

        return BaseResponseDto<ClosingSubmissionDto>.Ok(dto, action);
    }
}