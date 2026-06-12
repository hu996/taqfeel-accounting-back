using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class TenantAccessService(AppDbContext dbContext, ICurrentUserService currentUser) : ITenantAccessService
{
    public async Task<bool> CanAccessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantIsActive = await dbContext.Tenants.AnyAsync(x => x.Id == tenantId && x.IsActive, cancellationToken);
        if (!tenantIsActive)
        {
            return false;
        }

        if (currentUser.IsSuperAdmin)
        {
            return true;
        }

        if (currentUser.IsAccountingOfficeAdmin || currentUser.Roles.Contains(Roles.Accountant))
        {
            return await dbContext.UserTenantAccesses.AnyAsync(x => x.UserId == userId && x.TenantId == tenantId, cancellationToken);
        }

        return await dbContext.Users.AnyAsync(x => x.Id == userId && x.TenantId == tenantId && x.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantDto>> GetAccessibleTenantsAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return [];
        }

        IQueryable<Domain.Entities.Tenant> query = dbContext.Tenants.Where(x => x.IsActive);

        if (!currentUser.IsSuperAdmin)
        {
            if (currentUser.IsAccountingOfficeAdmin || currentUser.Roles.Contains(Roles.Accountant))
            {
                query = query.Where(x => dbContext.UserTenantAccesses.Any(a => a.UserId == userId && a.TenantId == x.Id));
            }
            else
            {
                var tenantId = await dbContext.Users.Where(x => x.Id == userId).Select(x => x.TenantId).FirstOrDefaultAsync(cancellationToken);
                query = query.Where(x => x.Id == tenantId);
            }
        }

        return await query.OrderBy(x => x.CompanyName).Select(x => ToDto(x)).ToListAsync(cancellationToken);
    }

    public async Task<TenantDto?> ValidateTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || !await CanAccessTenantAsync(userId, tenantId, cancellationToken))
        {
            return null;
        }

        return await dbContext.Tenants.Where(x => x.Id == tenantId && x.IsActive).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);
    }

    private static TenantDto ToDto(Domain.Entities.Tenant tenant) => new(
        tenant.Id,
        tenant.CompanyName,
        tenant.CommercialRegistrationNo,
        tenant.TaxNumber,
        tenant.Address,
        tenant.Phone,
        tenant.Email,
        tenant.IsActive);
}
