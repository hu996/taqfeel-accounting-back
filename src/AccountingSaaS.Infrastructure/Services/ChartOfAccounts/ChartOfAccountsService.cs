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
    IAuditLogService auditLog)
    : AccountingServiceBase(dbContext, currentTenant), IChartOfAccountsService
{
    public async Task<BaseResponseDto<AccountDto>> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        _ = TenantId;

        var codeExists = await DbContext.Accounts.AnyAsync(
            x => x.Code == request.Code,
            cancellationToken);

        if (codeExists)
        {
            return BaseResponseDto<AccountDto>.Fail("Account code already exists.");
        }

        if (request.ParentAccountId.HasValue)
        {
            var parentExists = await DbContext.Accounts.AnyAsync(
                x => x.Id == request.ParentAccountId,
                cancellationToken);

            if (!parentExists)
            {
                return BaseResponseDto<AccountDto>.Fail("Parent account was not found.");
            }
        }

        var account = new Account
        {
            Code = request.Code,
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
            "Account created",
            TenantId,
            currentUser.UserId,
            nameof(Account),
            account.Id.ToString(),
            newValues: account.Code,
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountDto>.Ok(
            AccountingMapper.ToDto(account),
            "Account created.");
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
            return BaseResponseDto<AccountDto>.Fail("Account was not found.");
        }

        var codeExists = await DbContext.Accounts.AnyAsync(
            x =>
                x.Id != id &&
                x.Code == request.Code,
            cancellationToken);

        if (codeExists)
        {
            return BaseResponseDto<AccountDto>.Fail("Account code already exists.");
        }

        if (request.ParentAccountId == id)
        {
            return BaseResponseDto<AccountDto>.Fail("Account cannot be its own parent.");
        }

        if (request.ParentAccountId.HasValue)
        {
            var parentExists = await DbContext.Accounts.AnyAsync(
                x => x.Id == request.ParentAccountId,
                cancellationToken);

            if (!parentExists)
            {
                return BaseResponseDto<AccountDto>.Fail("Parent account was not found.");
            }
        }

        var oldCode = account.Code;

        account.Code = request.Code;
        account.NameAr = request.NameAr;
        account.NameEn = request.NameEn;
        account.AccountType = request.AccountType;
        account.NormalBalance = request.NormalBalance;
        account.ParentAccountId = request.ParentAccountId;
        account.IsPostingAccount = request.IsPostingAccount;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Account updated",
            TenantId,
            currentUser.UserId,
            nameof(Account),
            id.ToString(),
            oldCode,
            account.Code,
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountDto>.Ok(
            AccountingMapper.ToDto(account),
            "Account updated.");
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
            return BaseResponseDto<AccountDto>.Fail("Account was not found.");
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
            return BaseResponseDto<AccountDto>.Fail("Account was not found.");
        }

        account.IsActive = active;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            active ? "Account activated" : "Account deactivated",
            TenantId,
            currentUser.UserId,
            nameof(Account),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<AccountDto>.Ok(
            AccountingMapper.ToDto(account),
            active ? "Account activated." : "Account deactivated.");
    }
}