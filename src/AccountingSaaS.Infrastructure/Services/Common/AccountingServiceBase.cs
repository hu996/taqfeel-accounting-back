using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public abstract class AccountingServiceBase(AppDbContext dbContext, ICurrentTenantService currentTenant)
{
    protected AppDbContext DbContext { get; } = dbContext;
    protected Guid TenantId
    {
        get
        {
            return currentTenant.TenantId
                ?? throw new UnauthorizedAccessException("A tenant session is required.");
        }
    }

    protected async Task<PaginatedResult<T>> ToPagedAsync<T>(IQueryable<T> query, PaginationRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.PageNumber, 1);
        var size = Math.Clamp(request.PageSize, 1, 200);
        return new PaginatedResult<T>
        {
            Items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken),
            TotalCount = await query.CountAsync(cancellationToken),
            PageNumber = page,
            PageSize = size
        };
    }

    protected async Task<bool> PeriodAllowsAccountingChangesAsync(Guid periodId, CancellationToken cancellationToken)
    {
        return await DbContext.AccountingPeriods.AnyAsync(
            x => x.Id == periodId && x.Status == AccountingPeriodStatus.Open,
            cancellationToken);
    }

    protected async Task<bool> PeriodIsClosedAsync(Guid periodId, CancellationToken cancellationToken)
    {
        return await DbContext.AccountingPeriods.AnyAsync(
            x => x.Id == periodId && x.Status == AccountingPeriodStatus.Closed,
            cancellationToken);
    }
}
