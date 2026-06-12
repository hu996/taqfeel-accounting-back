using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class AccountingPeriodService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog)
    : AccountingServiceBase(dbContext, currentTenant), IAccountingPeriodService
{
    public async Task<BaseResponseDto<AccountingPeriodDto>> CreateAsync(
        CreateAccountingPeriodRequest request,
        CancellationToken cancellationToken)
    {
        _ = TenantId;

        var year = await DbContext.FinancialYears.FindAsync(
            [request.FinancialYearId],
            cancellationToken);

        if (year is null)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("السنة المالية غير موجودة.");
        }

        if (request.StartDate < year.StartDate || request.EndDate > year.EndDate)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("يجب أن تكون الفترة المحاسبية داخل نطاق السنة المالية.");
        }

        var hasOverlap = await DbContext.AccountingPeriods.AnyAsync(
            x =>
                x.FinancialYearId == request.FinancialYearId &&
                request.StartDate <= x.EndDate &&
                request.EndDate >= x.StartDate,
            cancellationToken);

        if (hasOverlap)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("توجد فترة محاسبية أخرى تتداخل مع نفس التاريخ المحدد.");
        }

        var period = new AccountingPeriod
        {
            FinancialYearId = request.FinancialYearId,
            PeriodName = request.PeriodName,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        DbContext.AccountingPeriods.Add(period);

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "تم إنشاء فترة محاسبية",
            TenantId,
            currentUser.UserId,
            nameof(AccountingPeriod),
            period.Id.ToString(),
            newValues: period.PeriodName,
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountingPeriodDto>.Ok(
            AccountingMapper.ToDto(period),
            "تم إنشاء الفترة المحاسبية بنجاح.");
    }

    public async Task<BaseResponseDto<AccountingPeriodDto>> UpdateAsync(
        Guid id,
        UpdateAccountingPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var period = await DbContext.AccountingPeriods
            .Include(x => x.FinancialYear)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (period is null)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("الفترة المحاسبية غير موجودة.");
        }

        if (period.Status == AccountingPeriodStatus.Closed)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("لا يمكن تعديل فترة محاسبية مغلقة.");
        }

        if (
            request.StartDate < period.FinancialYear.StartDate ||
            request.EndDate > period.FinancialYear.EndDate)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("يجب أن تكون الفترة المحاسبية داخل نطاق السنة المالية.");
        }

        var hasOverlap = await DbContext.AccountingPeriods.AnyAsync(
            x =>
                x.Id != id &&
                x.FinancialYearId == period.FinancialYearId &&
                request.StartDate <= x.EndDate &&
                request.EndDate >= x.StartDate,
            cancellationToken);

        if (hasOverlap)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("توجد فترة محاسبية أخرى تتداخل مع نفس التاريخ المحدد.");
        }

        period.PeriodName = request.PeriodName;
        period.StartDate = request.StartDate;
        period.EndDate = request.EndDate;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "تم تعديل الفترة المحاسبية",
            TenantId,
            currentUser.UserId,
            nameof(AccountingPeriod),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountingPeriodDto>.Ok(
            AccountingMapper.ToDto(period),
            "تم تعديل الفترة المحاسبية بنجاح.");
    }

    public async Task<BaseResponseDto<AccountingPeriodDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var period = await DbContext.AccountingPeriods.FindAsync(
            [id],
            cancellationToken);

        if (period is null)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("الفترة المحاسبية غير موجودة.");
        }

        return BaseResponseDto<AccountingPeriodDto>.Ok(
            AccountingMapper.ToDto(period));
    }

    public async Task<BaseResponseDto<PaginatedResult<AccountingPeriodDto>>> GetPagedAsync(
        AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = DbContext.AccountingPeriods.AsQueryable();

        if (request.FinancialYearId.HasValue)
        {
            query = query.Where(x => x.FinancialYearId == request.FinancialYearId);
        }

        var result = await ToPagedAsync(
            query
                .OrderBy(x => x.StartDate)
                .Select(x => AccountingMapper.ToDto(x)),
            request,
            cancellationToken);

        return BaseResponseDto<PaginatedResult<AccountingPeriodDto>>.Ok(
            result,
            "تم تحميل الفترات المحاسبية بنجاح.");
    }

    public Task<BaseResponseDto<AccountingPeriodDto>> LockPeriodAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return SetStatusAsync(
            id,
            AccountingPeriodStatus.Locked,
            "تم قفل الفترة المحاسبية بنجاح.",
            cancellationToken);
    }

    public Task<BaseResponseDto<AccountingPeriodDto>> SubmitForReviewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return SetStatusAsync(
            id,
            AccountingPeriodStatus.SubmittedForReview,
            "تم إرسال الفترة المحاسبية للمراجعة بنجاح.",
            cancellationToken);
    }

    public Task<BaseResponseDto<AccountingPeriodDto>> ClosePeriodAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return SetStatusAsync(
            id,
            AccountingPeriodStatus.Closed,
            "تم إغلاق الفترة المحاسبية بنجاح.",
            cancellationToken);
    }

    public async Task<BaseResponseDto<AccountingPeriodDto>> ReopenPeriodAsync(
        Guid id,
        ReopenPeriodRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("يجب إدخال سبب إعادة فتح الفترة المحاسبية.");
        }

        var period = await DbContext.AccountingPeriods.FindAsync(
            [id],
            cancellationToken);

        if (period is null)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("الفترة المحاسبية غير موجودة.");
        }

        if (period.Status != AccountingPeriodStatus.Closed)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("يمكن إعادة فتح الفترات المحاسبية المغلقة فقط.");
        }

        period.Status = AccountingPeriodStatus.Open;
        period.ReopenedAt = DateTimeOffset.UtcNow;
        period.ReopenedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "تم إعادة فتح الفترة المحاسبية",
            TenantId,
            currentUser.UserId,
            nameof(AccountingPeriod),
            id.ToString(),
            newValues: request.Reason.Trim(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountingPeriodDto>.Ok(
            AccountingMapper.ToDto(period),
            "تم إعادة فتح الفترة المحاسبية بنجاح.");
    }

    private async Task<BaseResponseDto<AccountingPeriodDto>> SetStatusAsync(
        Guid id,
        AccountingPeriodStatus status,
        string action,
        CancellationToken cancellationToken)
    {
        var period = await DbContext.AccountingPeriods.FindAsync(
            [id],
            cancellationToken);

        if (period is null)
        {
            return BaseResponseDto<AccountingPeriodDto>.Fail("الفترة المحاسبية غير موجودة.");
        }

        if (status == AccountingPeriodStatus.Closed)
        {
            var canClose = await CanCloseAsync(id, cancellationToken);

            if (!canClose)
            {
                return BaseResponseDto<AccountingPeriodDto>.Fail("لا يمكن إغلاق الفترة المحاسبية قبل استيفاء متطلبات الإغلاق.");
            }
        }

        period.Status = status;

        var now = DateTimeOffset.UtcNow;

        if (status == AccountingPeriodStatus.Locked)
        {
            period.LockedAt = now;
            period.LockedByUserId = currentUser.UserId;
        }

        if (status == AccountingPeriodStatus.SubmittedForReview)
        {
            period.SubmittedAt = now;
            period.SubmittedByUserId = currentUser.UserId;
        }

        if (status == AccountingPeriodStatus.Closed)
        {
            period.ClosedAt = now;
            period.ClosedByUserId = currentUser.UserId;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            action,
            TenantId,
            currentUser.UserId,
            nameof(AccountingPeriod),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountingPeriodDto>.Ok(
            AccountingMapper.ToDto(period),
            action);
    }

    private async Task<bool> CanCloseAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var hasDraftJournalEntries = await DbContext.JournalEntries.AnyAsync(
            x =>
                x.AccountingPeriodId == periodId &&
                x.Status == JournalEntryStatus.Draft,
            cancellationToken);

        if (hasDraftJournalEntries)
        {
            return false;
        }

        var hasUnbalancedPostedEntries = await DbContext.JournalEntries.AnyAsync(
            x =>
                x.AccountingPeriodId == periodId &&
                x.Status == JournalEntryStatus.Posted &&
                x.TotalDebit != x.TotalCredit,
            cancellationToken);

        if (hasUnbalancedPostedEntries)
        {
            return false;
        }

        var hasPendingRequiredTasks = await DbContext.ClosingTasks.AnyAsync(
            x =>
                x.AccountingPeriodId == periodId &&
                x.IsRequired &&
                x.Status != ClosingTaskStatus.Approved &&
                x.Status != ClosingTaskStatus.NotApplicable,
            cancellationToken);

        if (hasPendingRequiredTasks)
        {
            return false;
        }

        var hasApprovedSubmission = await DbContext.ClosingSubmissions.AnyAsync(
            x =>
                x.AccountingPeriodId == periodId &&
                x.Status == ClosingSubmissionStatus.Approved,
            cancellationToken);

        return hasApprovedSubmission;
    }
}