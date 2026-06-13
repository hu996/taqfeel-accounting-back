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
    private readonly INumberSequenceService _numberSequence;

    public CostCenterService(
        AppDbContext dbContext,
        ICurrentTenantService currentTenant,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        INumberSequenceService numberSequence)
        : base(dbContext, currentTenant)
    {
        _currentUser = currentUser;
        _auditLog = auditLog;
        _numberSequence = numberSequence;
    }

    public async Task<BaseResponseDto<CostCenterDto>> CreateAsync(
        CreateCostCenterRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;

        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BaseResponseDto<CostCenterDto>.Fail("اسم مركز التكلفة مطلوب.");

        var costCenterNo = await _numberSequence.NextAsync("CostCenterNo", tenantId, cancellationToken);

        var costCenter = new CostCenter
        {
            CostCenterNo = costCenterNo,
            Code = $"CC-{costCenterNo:000000}",
            Name = name,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _currentUser.UserId
        };

        DbContext.CostCenters.Add(costCenter);
        await DbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "Created",
            tenantId,
            _currentUser.UserId,
            nameof(CostCenter),
            costCenter.Id.ToString(),
            newValues: costCenter.Code,
            cancellationToken: cancellationToken);

        return BaseResponseDto<CostCenterDto>.Ok(
            AccountingMapper.ToDto(costCenter),
            "تم إنشاء مركز التكلفة بنجاح.");
    }

    public async Task<BaseResponseDto<CostCenterDto>> UpdateAsync(
        Guid id,
        UpdateCostCenterRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;

        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BaseResponseDto<CostCenterDto>.Fail("اسم مركز التكلفة مطلوب.");

        var costCenter = await DbContext.CostCenters
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId &&
                !x.IsDeleted,
                cancellationToken);

        if (costCenter is null)
            return BaseResponseDto<CostCenterDto>.NotFound("مركز التكلفة غير موجود.");
        costCenter.Name = name;
        costCenter.UpdatedAt = DateTimeOffset.UtcNow;
        costCenter.UpdatedByUserId = _currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "Updated",
            tenantId,
            _currentUser.UserId,
            nameof(CostCenter),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<CostCenterDto>.Ok(
            AccountingMapper.ToDto(costCenter),
            "تم تحديث مركز التكلفة بنجاح.");
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
            return BaseResponseDto<CostCenterDto>.NotFound("مركز التكلفة غير موجود.");

        costCenter.IsActive = active;
        costCenter.UpdatedAt = DateTimeOffset.UtcNow;
        costCenter.UpdatedByUserId = _currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "Updated",
            tenantId,
            _currentUser.UserId,
            nameof(CostCenter),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<CostCenterDto>.Ok(
            AccountingMapper.ToDto(costCenter),
            active ? "تم تفعيل مركز التكلفة." : "تم إيقاف مركز التكلفة.");
    }
}
