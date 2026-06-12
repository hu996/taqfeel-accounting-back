using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class AccountingReportService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog)
    : AccountingServiceBase(dbContext, currentTenant), IAccountingReportService
{
    public async Task<BaseResponseDto<IReadOnlyList<TrialBalanceRowDto>>> GetTrialBalanceAsync(
        ReportDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (from, to) = await ResolveDateRangeAsync(request, cancellationToken);

        var rows = await DbContext.JournalEntryLines
            .Where(l =>
                l.JournalEntry.Status == JournalEntryStatus.Posted &&
                l.JournalEntry.EntryDate >= from &&
                l.JournalEntry.EntryDate <= to)
            .GroupBy(l => new
            {
                l.AccountId,
                l.Account.Code,
                l.Account.NameEn
            })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Code,
                g.Key.NameEn,
                PeriodDebit = g.Sum(x => x.Debit),
                PeriodCredit = g.Sum(x => x.Credit)
            })
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var accountIds = rows.Select(x => x.AccountId).ToList();

        var openingBalances = await DbContext.JournalEntryLines
            .Where(l =>
                accountIds.Contains(l.AccountId) &&
                l.JournalEntry.Status == JournalEntryStatus.Posted &&
                l.JournalEntry.EntryDate < from)
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Balance = g.Sum(x => x.Debit - x.Credit)
            })
            .ToDictionaryAsync(x => x.AccountId, x => x.Balance, cancellationToken);

        var result = rows
            .Select(row =>
            {
                var opening = openingBalances.GetValueOrDefault(row.AccountId);
                var closing = opening + row.PeriodDebit - row.PeriodCredit;

                return new TrialBalanceRowDto(
                    row.Code,
                    row.NameEn,
                    opening > 0 ? opening : 0,
                    opening < 0 ? Math.Abs(opening) : 0,
                    row.PeriodDebit,
                    row.PeriodCredit,
                    closing > 0 ? closing : 0,
                    closing < 0 ? Math.Abs(closing) : 0);
            })
            .ToList();

        await auditLog.LogAsync(
            "Trial balance viewed",
            TenantId,
            currentUser.UserId,
            cancellationToken: cancellationToken);

        return BaseResponseDto<IReadOnlyList<TrialBalanceRowDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<LedgerReportDto>> GetGeneralLedgerAsync(
        AccountReportRequest request,
        CancellationToken cancellationToken)
    {
        return await BuildLedgerAsync(
            request,
            "General ledger viewed",
            cancellationToken);
    }

    public async Task<BaseResponseDto<LedgerReportDto>> GetAccountStatementAsync(
        AccountReportRequest request,
        CancellationToken cancellationToken)
    {
        return await BuildLedgerAsync(
            request,
            "Account statement viewed",
            cancellationToken);
    }

    public async Task<BaseResponseDto<ClosingProgressDto>> GetClosingProgressAsync(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var tasks = await DbContext.ClosingTasks
            .Where(x => x.AccountingPeriodId == accountingPeriodId)
            .ToListAsync(cancellationToken);

        var submissionStatus = await DbContext.ClosingSubmissions
            .Where(x => x.AccountingPeriodId == accountingPeriodId)
            .Select(x => (ClosingSubmissionStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);

        var total = tasks.Count;

        var approved = tasks.Count(x =>
            x.Status == ClosingTaskStatus.Approved ||
            x.Status == ClosingTaskStatus.NotApplicable);

        var pending = tasks.Count(x =>
            x.Status == ClosingTaskStatus.Pending ||
            x.Status == ClosingTaskStatus.InProgress ||
            x.Status == ClosingTaskStatus.Submitted);

        var rejected = tasks.Count(x =>
            x.Status == ClosingTaskStatus.Rejected);

        var required = tasks.Count(x => x.IsRequired);

        var percentage = total == 0
            ? 0
            : Math.Round((decimal)approved / total * 100, 2);

        var dto = new ClosingProgressDto(
            total,
            required,
            approved,
            pending,
            rejected,
            percentage,
            submissionStatus);

        await auditLog.LogAsync(
            "Closing progress viewed",
            TenantId,
            currentUser.UserId,
            cancellationToken: cancellationToken);

        return BaseResponseDto<ClosingProgressDto>.Ok(dto);
    }

    private async Task<BaseResponseDto<LedgerReportDto>> BuildLedgerAsync(
        AccountReportRequest request,
        string action,
        CancellationToken cancellationToken)
    {
        var account = await DbContext.Accounts.FindAsync(
            [request.AccountId],
            cancellationToken);

        if (account is null)
        {
            return BaseResponseDto<LedgerReportDto>.Fail("Account was not found.");
        }

        var from = request.DateFrom ?? DateOnly.MinValue;
        var to = request.DateTo ?? DateOnly.MaxValue;

        var opening = await DbContext.JournalEntryLines
            .Where(l =>
                l.AccountId == request.AccountId &&
                l.JournalEntry.Status == JournalEntryStatus.Posted &&
                l.JournalEntry.EntryDate < from)
            .SumAsync(l => l.Debit - l.Credit, cancellationToken);

        var lines = await DbContext.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l =>
                l.AccountId == request.AccountId &&
                l.JournalEntry.Status == JournalEntryStatus.Posted &&
                l.JournalEntry.EntryDate >= from &&
                l.JournalEntry.EntryDate <= to)
            .OrderBy(l => l.JournalEntry.EntryDate)
            .ThenBy(l => l.JournalEntry.EntryNumber)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        var running = opening;

        var dtoLines = lines
            .Select(l =>
            {
                running += l.Debit - l.Credit;

                return new LedgerLineDto(
                    l.JournalEntry.EntryDate,
                    l.JournalEntry.EntryNumber,
                    l.Description ?? l.JournalEntry.Description,
                    l.Debit,
                    l.Credit,
                    running);
            })
            .ToList();

        await auditLog.LogAsync(
            action,
            TenantId,
            currentUser.UserId,
            nameof(Account),
            request.AccountId.ToString(),
            cancellationToken: cancellationToken);

        var dto = new LedgerReportDto(
            account.Code,
            account.NameEn,
            opening,
            dtoLines);

        return BaseResponseDto<LedgerReportDto>.Ok(dto);
    }

    private async Task<(DateOnly From, DateOnly To)> ResolveDateRangeAsync(
        ReportDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AccountingPeriodId.HasValue)
        {
            var period = await DbContext.AccountingPeriods.FindAsync(
                [request.AccountingPeriodId.Value],
                cancellationToken);

            if (period is not null)
            {
                return (period.StartDate, period.EndDate);
            }
        }

        var from = request.DateFrom ?? DateOnly.MinValue;
        var to = request.DateTo ?? DateOnly.MaxValue;

        return (from, to);
    }
}