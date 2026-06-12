using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class JournalEntryService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog)
    : AccountingServiceBase(dbContext, currentTenant), IJournalEntryService
{
    public async Task<BaseResponseDto<JournalEntryDto>> CreateDraftAsync(
        CreateJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        _ = TenantId;

        var validation = await ValidateJournalAsync(
            request.FinancialYearId,
            request.AccountingPeriodId,
            request.EntryDate,
            request.Lines,
            cancellationToken);

        if (!validation.Success)
        {
            return BaseResponseDto<JournalEntryDto>.Fail(validation.Message, validation.Errors);
        }

        var entry = new JournalEntry
        {
            FinancialYearId = request.FinancialYearId,
            AccountingPeriodId = request.AccountingPeriodId,
            EntryDate = request.EntryDate,
            Description = request.Description,
            EntryNumber = await NextEntryNumberAsync(request.FinancialYearId, cancellationToken),
            TotalDebit = request.Lines.Sum(x => x.Debit),
            TotalCredit = request.Lines.Sum(x => x.Credit)
        };

        foreach (var line in request.Lines)
        {
            entry.Lines.Add(new JournalEntryLine
            {
                AccountId = line.AccountId,
                CostCenterId = line.CostCenterId,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description
            });
        }

        DbContext.JournalEntries.Add(entry);

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Journal entry created",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            entry.Id.ToString(),
            newValues: entry.EntryNumber,
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(entry.Id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "Journal entry created.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> UpdateDraftAsync(
        Guid id,
        UpdateJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Journal entry was not found.");
        }

        if (entry.Status != JournalEntryStatus.Draft &&
            !currentUser.Permissions.Contains("JournalEntries.UpdatePostedJournalEntry"))
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Only draft entries can be updated.");
        }

        var validation = await ValidateJournalAsync(
            entry.FinancialYearId,
            entry.AccountingPeriodId,
            request.EntryDate,
            request.Lines,
            cancellationToken);

        if (!validation.Success)
        {
            return BaseResponseDto<JournalEntryDto>.Fail(validation.Message, validation.Errors);
        }

        entry.EntryDate = request.EntryDate;
        entry.Description = request.Description;
        entry.TotalDebit = request.Lines.Sum(x => x.Debit);
        entry.TotalCredit = request.Lines.Sum(x => x.Credit);

        DbContext.JournalEntryLines.RemoveRange(entry.Lines);

        entry.Lines = request.Lines
            .Select(line => new JournalEntryLine
            {
                AccountId = line.AccountId,
                CostCenterId = line.CostCenterId,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description
            })
            .ToList();

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Journal entry updated",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "Journal entry updated.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dto = await LoadDtoAsync(id, cancellationToken);

        if (dto is null)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Journal entry was not found.");
        }

        return BaseResponseDto<JournalEntryDto>.Ok(dto);
    }

    public async Task<BaseResponseDto<PaginatedResult<JournalEntryDto>>> GetPagedAsync(
      AccountingPagedRequest request,
      CancellationToken cancellationToken)
    {
        var query = DbContext.JournalEntries.IgnoreQueryFilters()
            .Include(x => x.Lines)
            .ThenInclude(x => x.Account)
            .AsQueryable();

        if (request.FinancialYearId.HasValue)
        {
            query = query.Where(x => x.FinancialYearId == request.FinancialYearId.Value);
        }

        if (request.AccountingPeriodId.HasValue)
        {
            query = query.Where(x => x.AccountingPeriodId == request.AccountingPeriodId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.EntryNumber.Contains(search) ||
                x.Description.Contains(search));
        }

        var result = await ToPagedAsync(
            query
                .OrderByDescending(x => x.EntryDate)
                .Select(x => AccountingMapper.ToDto(x)),
            request,
            cancellationToken);

        return BaseResponseDto<PaginatedResult<JournalEntryDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<JournalEntryDto>> PostAsync(
        Guid id,
        PostJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Journal entry was not found.");
        }

        var periodAllowsChanges = await PeriodAllowsAccountingChangesAsync(
            entry.AccountingPeriodId,
            cancellationToken);

        if (!periodAllowsChanges)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Cannot post in a locked or closed period.");
        }

        if (entry.Lines.Count < 2 || entry.TotalDebit != entry.TotalCredit)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Journal entry is not balanced.");
        }

        entry.Status = JournalEntryStatus.Posted;
        entry.PostedAt = DateTimeOffset.UtcNow;
        entry.PostedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Journal entry posted",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            newValues: request.Notes,
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "Journal entry posted.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> ReverseAsync(
        Guid id,
        ReverseJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries.FindAsync([id], cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Journal entry was not found.");
        }

        var periodAllowsChanges = await PeriodAllowsAccountingChangesAsync(
            entry.AccountingPeriodId,
            cancellationToken);

        if (!periodAllowsChanges)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Cannot reverse in a locked or closed period.");
        }

        entry.Status = JournalEntryStatus.Reversed;
        entry.ReversedAt = DateTimeOffset.UtcNow;
        entry.ReversedByUserId = currentUser.UserId;
        entry.ReversalReason = request.Reason;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Journal entry reversed",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            newValues: request.Reason,
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "Journal entry reversed.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> CancelAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries.FindAsync([id], cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Journal entry was not found.");
        }

        if (entry.Status != JournalEntryStatus.Draft)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Only draft entries can be cancelled.");
        }

        var periodAllowsChanges = await PeriodAllowsAccountingChangesAsync(
            entry.AccountingPeriodId,
            cancellationToken);

        if (!periodAllowsChanges)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("Cannot cancel entries in a locked or closed period.");
        }

        entry.Status = JournalEntryStatus.Cancelled;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Journal entry cancelled",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "Journal entry cancelled.");
    }

    private async Task<BaseResponseDto<object>> ValidateJournalAsync(
        Guid yearId,
        Guid periodId,
        DateOnly entryDate,
        IReadOnlyList<JournalEntryLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var period = await DbContext.AccountingPeriods
            .FirstOrDefaultAsync(
                x => x.Id == periodId && x.FinancialYearId == yearId,
                cancellationToken);

        if (period is null)
        {
            return BaseResponseDto<object>.Fail("Accounting period was not found.");
        }

        if (period.Status is AccountingPeriodStatus.Closed or AccountingPeriodStatus.Locked)
        {
            return BaseResponseDto<object>.Fail("Cannot edit accounting data in locked or closed periods.");
        }

        if (entryDate < period.StartDate || entryDate > period.EndDate)
        {
            return BaseResponseDto<object>.Fail("Entry date must be inside the accounting period.");
        }

        if (lines.Count < 2)
        {
            return BaseResponseDto<object>.Fail("Journal entry must contain at least two lines.");
        }

        var totalDebit = lines.Sum(x => x.Debit);
        var totalCredit = lines.Sum(x => x.Credit);

        if (totalDebit != totalCredit)
        {
            return BaseResponseDto<object>.Fail("Journal entry is not balanced.");
        }

        if (totalDebit <= 0)
        {
            return BaseResponseDto<object>.Fail("Journal entry total must be greater than zero.");
        }

        var hasInvalidLine = lines.Any(x =>
            x.Debit < 0 ||
            x.Credit < 0 ||
            x.Debit > 0 && x.Credit > 0 ||
            x.Debit == 0 && x.Credit == 0);

        if (hasInvalidLine)
        {
            return BaseResponseDto<object>.Fail("Each line must contain either debit or credit only.");
        }

        var accountIds = lines
            .Select(x => x.AccountId)
            .Distinct()
            .ToList();

        var validAccountsCount = await DbContext.Accounts
            .CountAsync(
                x => accountIds.Contains(x.Id) &&
                     x.IsActive &&
                     x.IsPostingAccount,
                cancellationToken);

        if (validAccountsCount != accountIds.Count)
        {
            return BaseResponseDto<object>.Fail("All accounts must be active posting accounts.");
        }

        var costCenterIds = lines
            .Where(x => x.CostCenterId.HasValue)
            .Select(x => x.CostCenterId!.Value)
            .Distinct()
            .ToList();

        if (costCenterIds.Count > 0)
        {
            var validCostCentersCount = await DbContext.CostCenters
                .CountAsync(
                    x => costCenterIds.Contains(x.Id) && x.IsActive,
                    cancellationToken);

            if (validCostCentersCount != costCenterIds.Count)
            {
                return BaseResponseDto<object>.Fail("All cost centers must be active.");
            }
        }

        return BaseResponseDto<object>.Ok(null);
    }

    private async Task<string> NextEntryNumberAsync(
        Guid financialYearId,
        CancellationToken cancellationToken)
    {
        var count = await DbContext.JournalEntries
            .CountAsync(x => x.FinancialYearId == financialYearId, cancellationToken);

        count++;

        return $"JE-{DateTimeOffset.UtcNow:yyyy}-{count:00000}";
    }

    private async Task<JournalEntryDto?> LoadDtoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dto = await DbContext.JournalEntries
            .Include(x => x.Lines)
            .ThenInclude(x => x.Account)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CostCenter)
            .Where(x => x.Id == id)
            .Select(x => AccountingMapper.ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);

        return dto;
    }
}