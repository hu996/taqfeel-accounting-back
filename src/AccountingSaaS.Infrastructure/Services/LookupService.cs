using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Enums;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class LookupService(AppDbContext dbContext, ICurrentTenantService currentTenant, ICurrentUserService currentUser) : ILookupService
{
    public Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetLookupAsync(LookupType lookupType, LookupRequest request, CancellationToken cancellationToken = default) =>
        lookupType switch
        {
            LookupType.Tenants => GetTenantsAsync(request, cancellationToken),
            LookupType.Users => GetUsersAsync(request, cancellationToken),
            LookupType.Roles => GetRolesAsync(request, cancellationToken),
            LookupType.Permissions => GetPermissionsAsync(request, cancellationToken),
            LookupType.FinancialYears => GetFinancialYearsAsync(request, cancellationToken),
            LookupType.AccountingPeriods => GetAccountingPeriodsAsync(request, cancellationToken),
            LookupType.ChartOfAccounts => GetChartOfAccountsAsync(request, cancellationToken),
            LookupType.PostingAccounts => GetPostingAccountsAsync(request, cancellationToken),
            LookupType.ParentAccounts => GetParentAccountsAsync(request, cancellationToken),
            LookupType.CostCenters => GetCostCentersAsync(request, cancellationToken),
            LookupType.Customers or LookupType.Suppliers => EmptyAsync(),
            LookupType.DocumentTypes => EnumAsync<DocumentType>(),
            LookupType.AccountTypes => EnumAsync<AccountType>(),
            LookupType.NormalBalances => EnumAsync<NormalBalance>(),
            LookupType.JournalEntryStatuses => EnumAsync<JournalEntryStatus>(),
            LookupType.AccountingPeriodStatuses => EnumAsync<AccountingPeriodStatus>(),
            LookupType.ClosingTaskStatuses => EnumAsync<ClosingTaskStatus>(),
            LookupType.ClosingSubmissionStatuses => EnumAsync<ClosingSubmissionStatus>(),
            LookupType.ImportTypes => EnumAsync<ImportType>(),
            _ => EmptyAsync()
        };

    public async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetTenantsAsync(LookupRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        if (currentUser.UserId is not { } userId)
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("User is not authenticated.");
        }

        var query = dbContext.Tenants.AsNoTracking().Where(x => x.IsActive);
        if (!currentUser.IsSuperAdmin)
        {
            if (currentUser.IsAccountingOfficeAdmin || currentUser.Roles.Contains(Roles.Accountant, StringComparer.OrdinalIgnoreCase))
            {
                query = query.Where(x => dbContext.UserTenantAccesses.Any(a => a.UserId == userId && a.TenantId == x.Id));
            }
            else
            {
                var tenantId = await dbContext.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.TenantId).FirstOrDefaultAsync(cancellationToken);
                query = query.Where(x => x.Id == tenantId);
            }
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x =>
                x.CompanyName.Contains(normalized.Search)
                || (x.TaxNumber != null && x.TaxNumber.Contains(normalized.Search))
                || (x.CommercialRegistrationNo != null && x.CommercialRegistrationNo.Contains(normalized.Search)));
        }

        var result = await query.OrderBy(x => x.CompanyName).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = x.CompanyName,
            LabelAr = x.CompanyName,
            LabelEn = x.CompanyName,
            Code = x.TaxNumber ?? x.CommercialRegistrationNo,
            Extra = x.Email ?? x.Phone,
            IsActive = x.IsActive
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetUsersAsync(LookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission("Users.View", "ClosingTasks.Manage"))
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("Permission denied.");
        }

        var normalized = Normalize(request);
        var query = dbContext.Users.AsNoTracking().AsQueryable();
        if (normalized.ActiveOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!currentUser.IsSuperAdmin)
        {
            if (!currentTenant.IsTenantSelected)
            {
                return TenantRequired<IReadOnlyList<LookupDto>>();
            }

            query = query.Where(x =>
                x.TenantId == currentTenant.TenantId
                || dbContext.UserTenantAccesses.Any(a => a.UserId == x.Id && a.TenantId == currentTenant.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x =>
                x.FullName.Contains(normalized.Search)
                || x.Email!.Contains(normalized.Search)
                || (x.PhoneNumber != null && x.PhoneNumber.Contains(normalized.Search)));
        }

        var result = await query.OrderBy(x => x.FullName).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = string.IsNullOrWhiteSpace(x.FullName) ? x.Email! : x.FullName,
            LabelAr = x.FullName,
            LabelEn = x.FullName,
            Code = x.Email,
            Extra = x.Email,
            IsActive = x.IsActive
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetRolesAsync(LookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission("Users.AssignRoles"))
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("Permission denied.");
        }

        var normalized = Normalize(request);
        var query = dbContext.Roles.AsNoTracking().AsQueryable();
        if (!currentUser.IsSuperAdmin)
        {
            query = query.Where(x =>
                x.Name == Roles.Accountant
                || x.Name == Roles.Reviewer
                || x.Name == Roles.CompanyOwner
                || x.Name == Roles.CompanyUser);
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x => x.Name!.Contains(normalized.Search));
        }

        var result = await query.OrderBy(x => x.Name).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = x.Name!,
            LabelEn = x.Name,
            Code = x.Name,
            IsActive = true
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetPermissionsAsync(LookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission("Users.AssignPermissions"))
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("Permission denied.");
        }

        var normalized = Normalize(request);
        var query = dbContext.Permissions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x => x.Name.Contains(normalized.Search) || x.Category.Contains(normalized.Search));
        }

        var result = await query.OrderBy(x => x.Category).ThenBy(x => x.Name).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = x.Name,
            LabelEn = x.Name,
            Code = x.Category,
            Extra = x.Description,
            IsActive = true
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetFinancialYearsAsync(LookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission("FinancialYears.View", "Accounting.View"))
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("Permission denied.");
        }

        if (!currentTenant.IsTenantSelected)
        {
            return TenantRequired<IReadOnlyList<LookupDto>>();
        }

        var normalized = Normalize(request);
        var query = dbContext.FinancialYears.AsNoTracking().AsQueryable();
        if (normalized.ActiveOnly)
        {
            query = query.Where(x => x.Status != FinancialYearStatus.Closed);
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x => x.YearName.Contains(normalized.Search));
        }

        var result = await query.OrderByDescending(x => x.StartDate).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = x.YearName,
            LabelEn = x.YearName,
            Extra = x.StartDate + " - " + x.EndDate,
            IsActive = x.Status != FinancialYearStatus.Closed
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetAccountingPeriodsAsync(LookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission("AccountingPeriods.View", "Accounting.View"))
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("Permission denied.");
        }

        if (!currentTenant.IsTenantSelected)
        {
            return TenantRequired<IReadOnlyList<LookupDto>>();
        }

        var normalized = Normalize(request);
        var query = dbContext.AccountingPeriods.AsNoTracking().AsQueryable();
        if (normalized.FinancialYearId.HasValue)
        {
            query = query.Where(x => x.FinancialYearId == normalized.FinancialYearId);
        }

        if (normalized.ActiveOnly)
        {
            query = query.Where(x => x.Status != AccountingPeriodStatus.Closed);
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x => x.PeriodName.Contains(normalized.Search));
        }

        var result = await query.OrderBy(x => x.StartDate).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = x.PeriodName,
            LabelEn = x.PeriodName,
            Extra = x.Status + " | " + x.StartDate + " - " + x.EndDate,
            IsActive = x.Status != AccountingPeriodStatus.Closed
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    public Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetChartOfAccountsAsync(LookupRequest request, CancellationToken cancellationToken = default) =>
        GetAccountsAsync(request, postingOnly: false, parentOnly: false, cancellationToken);

    public Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetPostingAccountsAsync(LookupRequest request, CancellationToken cancellationToken = default) =>
        GetAccountsAsync(request, postingOnly: true, parentOnly: false, cancellationToken);

    public Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetParentAccountsAsync(LookupRequest request, CancellationToken cancellationToken = default) =>
        GetAccountsAsync(request, postingOnly: false, parentOnly: true, cancellationToken);

    public async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetCostCentersAsync(LookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission("CostCenters.View", "Accounting.View"))
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("Permission denied.");
        }

        if (!currentTenant.IsTenantSelected)
        {
            return TenantRequired<IReadOnlyList<LookupDto>>();
        }

        var normalized = Normalize(request);
        var query = dbContext.CostCenters.AsNoTracking().AsQueryable();
        if (normalized.ActiveOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x => x.Code.Contains(normalized.Search) || x.Name.Contains(normalized.Search));
        }

        var result = await query.OrderBy(x => x.Code).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = x.Code + " - " + x.Name,
            LabelAr = x.Name,
            LabelEn = x.Name,
            Code = x.Code,
            IsActive = x.IsActive
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    private async Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetAccountsAsync(LookupRequest request, bool postingOnly, bool parentOnly, CancellationToken cancellationToken)
    {
        var requiredPermissions = postingOnly
            ? ["JournalEntries.Create", "JournalEntries.Update", "Accounting.View"]
            : new[] { "ChartOfAccounts.View", "Accounting.View" };

        if (!HasAnyPermission(requiredPermissions))
        {
            return BaseResponseDto<IReadOnlyList<LookupDto>>.Fail("Permission denied.");
        }

        if (!currentTenant.IsTenantSelected)
        {
            return TenantRequired<IReadOnlyList<LookupDto>>();
        }

        var normalized = Normalize(request);
        var query = dbContext.Accounts.AsNoTracking().AsQueryable();
        if (normalized.ActiveOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        if (postingOnly)
        {
            query = query.Where(x => x.IsPostingAccount && x.IsActive);
        }

        if (parentOnly)
        {
            query = query.Where(x => !x.IsPostingAccount);
            if (normalized.ParentId.HasValue)
            {
                var excludedIds = await GetAccountAndDescendantIdsAsync(normalized.ParentId.Value, cancellationToken);
                query = query.Where(x => !excludedIds.Contains(x.Id));
            }
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            query = query.Where(x => x.Code.Contains(normalized.Search) || x.NameAr.Contains(normalized.Search) || x.NameEn.Contains(normalized.Search));
        }

        var result = await query.OrderBy(x => x.Code).Take(normalized.Take).Select(x => new LookupDto
        {
            Id = x.Id,
            Label = x.Code + " - " + (string.IsNullOrWhiteSpace(x.NameAr) ? x.NameEn : x.NameAr),
            LabelAr = x.NameAr,
            LabelEn = x.NameEn,
            Code = x.Code,
            Extra = x.AccountType.ToString(),
            IsActive = x.IsActive
        }).ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(result);
    }

    private async Task<HashSet<Guid>> GetAccountAndDescendantIdsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var excludedIds = new HashSet<Guid> { accountId };
        var frontier = new List<Guid> { accountId };

        while (frontier.Count > 0)
        {
            var children = await dbContext.Accounts.AsNoTracking()
                .Where(x => x.ParentAccountId.HasValue && frontier.Contains(x.ParentAccountId.Value))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            frontier = children.Where(excludedIds.Add).ToList();
        }

        return excludedIds;
    }

    private bool HasAnyPermission(params string[] permissions) =>
        currentUser.IsSuperAdmin || permissions.Any(p => currentUser.Permissions.Contains(p, StringComparer.OrdinalIgnoreCase));

    private static LookupRequest Normalize(LookupRequest request)
    {
        var trimmedSearch = request.Search?.Trim();
        return new LookupRequest
        {
            Search = string.IsNullOrWhiteSpace(trimmedSearch) ? null : trimmedSearch[..Math.Min(trimmedSearch.Length, 100)],
            Take = Math.Clamp(request.Take <= 0 ? 50 : request.Take, 1, 200),
            ActiveOnly = request.ActiveOnly,
            FinancialYearId = request.FinancialYearId,
            AccountingPeriodId = request.AccountingPeriodId,
            ParentId = request.ParentId,
            Type = request.Type?.Trim()
        };
    }

    private static Task<BaseResponseDto<IReadOnlyList<LookupDto>>> EmptyAsync() =>
        Task.FromResult(BaseResponseDto<IReadOnlyList<LookupDto>>.Ok([]));

    private static Task<BaseResponseDto<IReadOnlyList<LookupDto>>> EnumAsync<TEnum>() where TEnum : struct, Enum
    {
        IReadOnlyList<LookupDto> values = Enum.GetValues<TEnum>().Select(value => new LookupDto
        {
            Id = Guid.Empty,
            Label = value.ToString(),
            LabelAr = ArabicLabel(value),
            LabelEn = value.ToString(),
            Code = Convert.ToInt32(value).ToString(),
            Extra = value.ToString(),
            IsActive = true
        }).ToList();
        return Task.FromResult(BaseResponseDto<IReadOnlyList<LookupDto>>.Ok(values));
    }

    private static BaseResponseDto<T> TenantRequired<T>() =>
        BaseResponseDto<T>.Fail("Please select a company first.", ["Tenant context is required."]);

    private static string ArabicLabel<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value switch
        {
            AccountType.Asset => "الأصول",
            AccountType.Liability => "الالتزامات",
            AccountType.Equity => "حقوق الملكية",
            AccountType.Revenue => "الإيرادات",
            AccountType.Expense => "المصروفات",
            NormalBalance.Debit => "مدين",
            NormalBalance.Credit => "دائن",
            JournalEntryStatus.Draft => "مسودة",
            JournalEntryStatus.Posted => "مرحل",
            JournalEntryStatus.Reversed => "معكوس",
            JournalEntryStatus.Cancelled => "ملغي",
            AccountingPeriodStatus.Open => "مفتوحة",
            AccountingPeriodStatus.Locked => "مقفلة مؤقتا",
            AccountingPeriodStatus.SubmittedForReview => "مرسلة للمراجعة",
            AccountingPeriodStatus.UnderReview => "تحت المراجعة",
            AccountingPeriodStatus.Rejected => "مرفوضة",
            AccountingPeriodStatus.Closed => "مغلقة",
            ClosingTaskStatus.Pending => "معلقة",
            ClosingTaskStatus.InProgress => "قيد التنفيذ",
            ClosingTaskStatus.Submitted => "مرسلة",
            ClosingTaskStatus.Approved => "معتمدة",
            ClosingTaskStatus.Rejected => "مرفوضة",
            ClosingTaskStatus.NotApplicable => "غير مطبقة",
            ClosingSubmissionStatus.Draft => "مسودة",
            ClosingSubmissionStatus.Submitted => "مرسلة",
            ClosingSubmissionStatus.UnderReview => "تحت المراجعة",
            ClosingSubmissionStatus.Approved => "معتمدة",
            ClosingSubmissionStatus.Rejected => "مرفوضة",
            ClosingSubmissionStatus.Closed => "مغلقة",
            ClosingSubmissionStatus.Reopened => "أعيد فتحها",
            ImportType.ChartOfAccounts => "دليل الحسابات",
            ImportType.OpeningBalances => "أرصدة أول المدة",
            ImportType.Customers => "العملاء",
            ImportType.Suppliers => "الموردين",
            ImportType.CostCenters => "مراكز التكلفة",
            ImportType.JournalEntries => "القيود اليومية",
            ImportType.BankTransactions => "حركات البنك",
            _ => value.ToString()
        };
}
