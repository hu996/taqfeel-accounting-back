using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class CostCenterService : AccountingServiceBase, ICostCenterService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CostCenterService(
        AppDbContext dbContext,
        ICurrentTenantService currentTenant,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
        : base(dbContext, currentTenant)
    {
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<BaseResponseDto<CostCenterDto>> CreateAsync(
        CreateCostCenterRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;

        var code = request.Code?.Trim();
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(code))
            return BaseResponseDto<CostCenterDto>.Fail("Cost center code is required.");

        if (string.IsNullOrWhiteSpace(name))
            return BaseResponseDto<CostCenterDto>.Fail("Cost center name is required.");

        var exists = await DbContext.CostCenters
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.Code == code,
                cancellationToken);

        if (exists)
            return BaseResponseDto<CostCenterDto>.Fail("Cost center code already exists.");

        var costCenter = new CostCenter
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _currentUser.UserId
        };

        DbContext.CostCenters.Add(costCenter);
        await DbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "Cost center created",
            tenantId,
            _currentUser.UserId,
            nameof(CostCenter),
            costCenter.Id.ToString(),
            newValues: costCenter.Code,
            cancellationToken: cancellationToken);

        return BaseResponseDto<CostCenterDto>.Ok(
            AccountingMapper.ToDto(costCenter),
            "Cost center created.");
    }

    public async Task<BaseResponseDto<CostCenterDto>> UpdateAsync(
        Guid id,
        UpdateCostCenterRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;

        var code = request.Code?.Trim();
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(code))
            return BaseResponseDto<CostCenterDto>.Fail("Cost center code is required.");

        if (string.IsNullOrWhiteSpace(name))
            return BaseResponseDto<CostCenterDto>.Fail("Cost center name is required.");

        var costCenter = await DbContext.CostCenters
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId &&
                !x.IsDeleted,
                cancellationToken);

        if (costCenter is null)
            return BaseResponseDto<CostCenterDto>.Fail("Cost center was not found.");

        var codeExists = await DbContext.CostCenters
            .AnyAsync(x =>
                x.Id != id &&
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.Code == code,
                cancellationToken);

        if (codeExists)
            return BaseResponseDto<CostCenterDto>.Fail("Cost center code already exists.");

        costCenter.Code = code;
        costCenter.Name = name;
        costCenter.UpdatedAt = DateTimeOffset.UtcNow;
        costCenter.UpdatedByUserId = _currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "Cost center updated",
            tenantId,
            _currentUser.UserId,
            nameof(CostCenter),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<CostCenterDto>.Ok(
            AccountingMapper.ToDto(costCenter),
            "Cost center updated.");
    }

    public async Task<BaseResponseDto<PaginatedResult<CostCenterDto>>> GetPagedAsync(
        AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;

        var query = DbContext.CostCenters
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search));
        }

        var pagedEntities = await ToPagedAsync(
            query.OrderBy(x => x.Code),
            request,
            cancellationToken);

        var pagedDtos = new PaginatedResult<CostCenterDto>
        {
            Items = pagedEntities.Items
                .Select(AccountingMapper.ToDto)
                .ToList(),

            PageNumber = pagedEntities.PageNumber,
            PageSize = pagedEntities.PageSize,
            TotalCount = pagedEntities.TotalCount
        };

        return BaseResponseDto<PaginatedResult<CostCenterDto>>.Ok(pagedDtos);
    }

    public async Task<BaseResponseDto<CostCenterDto>> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await SetActiveAsync(id, true, cancellationToken);
    }

    public async Task<BaseResponseDto<CostCenterDto>> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await SetActiveAsync(id, false, cancellationToken);
    }

    private async Task<BaseResponseDto<CostCenterDto>> SetActiveAsync(
        Guid id,
        bool active,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;

        var costCenter = await DbContext.CostCenters
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId &&
                !x.IsDeleted,
                cancellationToken);

        if (costCenter is null)
            return BaseResponseDto<CostCenterDto>.Fail("Cost center was not found.");

        costCenter.IsActive = active;
        costCenter.UpdatedAt = DateTimeOffset.UtcNow;
        costCenter.UpdatedByUserId = _currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            active ? "Cost center activated" : "Cost center deactivated",
            tenantId,
            _currentUser.UserId,
            nameof(CostCenter),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<CostCenterDto>.Ok(
            AccountingMapper.ToDto(costCenter),
            active ? "Cost center activated." : "Cost center deactivated.");
    }
}
