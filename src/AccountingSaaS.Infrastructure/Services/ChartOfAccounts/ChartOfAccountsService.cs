using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ChartOfAccountsService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog,
    INumberSequenceService numberSequence)
    : AccountingServiceBase(dbContext, currentTenant), IChartOfAccountsService
{
    public async Task<BaseResponseDto<AccountDto>> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        _ = TenantId;

        Account? parent = null;
        if (request.ParentAccountId.HasValue)
        {
            parent = await DbContext.Accounts.FirstOrDefaultAsync(
                x => x.Id == request.ParentAccountId,
                cancellationToken);

            if (parent is null)
            {
                return BaseResponseDto<AccountDto>.NotFound("الحساب الأب غير موجود.");
            }
        }

        var accountNo = await numberSequence.NextAsync("AccountNo", TenantId, cancellationToken);
        var parentPart = parent is null ? 1 : parent.AccountNo;
        var codeSequence = await numberSequence.NextAsync(
            $"AccountCode:{(int)request.AccountType}:{parent?.Id.ToString() ?? "ROOT"}",
            TenantId,
            cancellationToken);

        var account = new Account
        {
            AccountNo = accountNo,
            Code = $"{(int)request.AccountType}-{parentPart:000}-{codeSequence:0000}",
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            AccountType = request.AccountType,
            NormalBalance = request.NormalBalance,
            ParentAccountId = request.ParentAccountId,
            IsPostingAccount = request.IsPostingAccount,
            IsActive = true
        };

        DbContext.Accounts.Add(account);

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Created",
            TenantId,
            currentUser.UserId,
            nameof(Account),
            account.Id.ToString(),
            newValues: account.Code,
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountDto>.Ok(
            AccountingMapper.ToDto(account),
            "تم إنشاء الحساب بنجاح.");
    }

    public async Task<BaseResponseDto<AccountDto>> UpdateAccountAsync(
        Guid id,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await DbContext.Accounts.FindAsync(
            [id],
            cancellationToken);

        if (account is null)
        {
            return BaseResponseDto<AccountDto>.NotFound("الحساب غير موجود.");
        }

        if (request.ParentAccountId == id)
        {
            return BaseResponseDto<AccountDto>.Fail("لا يمكن أن يكون الحساب أبًا لنفسه.");
        }

        if (request.ParentAccountId.HasValue)
        {
            var parentExists = await DbContext.Accounts.AnyAsync(
                x => x.Id == request.ParentAccountId,
                cancellationToken);

            if (!parentExists)
            {
                return BaseResponseDto<AccountDto>.NotFound("الحساب الأب غير موجود.");
            }
        }

        var oldCode = account.Code;

        account.NameAr = request.NameAr;
        account.NameEn = request.NameEn;
        account.AccountType = request.AccountType;
        account.NormalBalance = request.NormalBalance;
        account.ParentAccountId = request.ParentAccountId;
        account.IsPostingAccount = request.IsPostingAccount;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Updated",
            TenantId,
            currentUser.UserId,
            nameof(Account),
            id.ToString(),
            oldCode,
            account.Code,
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountDto>.Ok(
            AccountingMapper.ToDto(account),
            "تم تحديث الحساب بنجاح.");
    }

    public async Task<BaseResponseDto<AccountDto>> GetAccountAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var account = await DbContext.Accounts.FindAsync(
            [id],
            cancellationToken);

        if (account is null)
        {
            return BaseResponseDto<AccountDto>.NotFound("الحساب غير موجود.");
        }

        return BaseResponseDto<AccountDto>.Ok(
            AccountingMapper.ToDto(account));
    }

    public async Task<BaseResponseDto<IReadOnlyList<AccountDto>>> GetTreeAsync(
        CancellationToken cancellationToken)
    {
        var accounts = await DbContext.Accounts
            .OrderBy(x => x.Code)
            .Select(x => AccountingMapper.ToDto(x))
            .ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<AccountDto>>.Ok(accounts);
    }

    public async Task<BaseResponseDto<PaginatedResult<AccountDto>>> GetPagedAsync(
        AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = DbContext.Accounts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(
                x =>
                    x.Code.Contains(request.Search) ||
                    x.NameEn.Contains(request.Search));
        }

        var result = await ToPagedAsync(
            query
                .OrderBy(x => x.Code)
                .Select(x => AccountingMapper.ToDto(x)),
            request,
            cancellationToken);

        return BaseResponseDto<PaginatedResult<AccountDto>>.Ok(result);
    }

    public Task<BaseResponseDto<AccountDto>> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return SetActiveAsync(
            id,
            true,
            cancellationToken);
    }

    public Task<BaseResponseDto<AccountDto>> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return SetActiveAsync(
            id,
            false,
            cancellationToken);
    }

    private async Task<BaseResponseDto<AccountDto>> SetActiveAsync(
        Guid id,
        bool active,
        CancellationToken cancellationToken)
    {
        var account = await DbContext.Accounts.FindAsync(
            [id],
            cancellationToken);

        if (account is null)
        {
            return BaseResponseDto<AccountDto>.NotFound("الحساب غير موجود.");
        }

        account.IsActive = active;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Updated",
            TenantId,
            currentUser.UserId,
            nameof(Account),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountDto>.Ok(
            AccountingMapper.ToDto(account),
            active ? "تم تفعيل الحساب." : "تم إيقاف الحساب.");
    }
}
