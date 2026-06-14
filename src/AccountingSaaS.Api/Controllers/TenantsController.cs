using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TenantsController(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ITenantAccessService tenantAccessService,
    IAuditLogService auditLogService,
    INumberSequenceService numberSequence) : AccountingControllerBase
{
    [HttpGet("GetTenants")]
    [HasPermission("Tenants.View")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var query = dbContext.Tenants.AsQueryable();
        if (!currentUser.IsSuperAdmin && (currentUser.IsAccountingOfficeAdmin || currentUser.Roles.Contains(Roles.Accountant)))
        {
            query = query.Where(x => dbContext.UserTenantAccesses.Any(a => a.UserId == currentUser.UserId && a.TenantId == x.Id));
        }
        else if (!currentUser.IsSuperAdmin)
        {
            query = query.Where(x => x.Id == dbContext.Users.Where(u => u.Id == currentUser.UserId).Select(u => u.TenantId).FirstOrDefault());
        }

        var tenants = await query.OrderBy(x => x.CompanyName).Select(x => ToDto(x)).ToListAsync(cancellationToken);
        return ApiResult(BaseResponseDto<IReadOnlyList<TenantDto>>.Ok(tenants));
    }

    [HttpGet("GetTenantById/{id:guid}")]
    [HasPermission("Tenants.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!await CanAccessTenantAsync(id, cancellationToken))
        {
            return ForbiddenResponse();
        }

        var tenant = await dbContext.Tenants.Where(x => x.Id == id).Select(x => ToDto(x)).FirstOrDefaultAsync(cancellationToken);
        return ApiResult(tenant is null
            ? BaseResponseDto<TenantDto>.NotFound("الشركة غير موجودة.")
            : BaseResponseDto<TenantDto>.Ok(tenant));
    }

    [HttpPost("AddTenant")]
    [HasPermission("Tenants.Create")]
    public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin && !currentUser.IsAccountingOfficeAdmin)
        {
            return ForbiddenResponse();
        }

        var tenantNo = await numberSequence.NextAsync(
            "TenantNo",
            null,
            cancellationToken);
        var tenant = new Tenant
        {
            TenantNo = tenantNo,
            CompanyName = request.CompanyName,
            CompanyCode = $"COMP-{tenantNo:000}",
            CompanyNameAr = request.CompanyName,
            CompanyNameEn = request.CompanyName,
            CommercialRegistrationNo = request.CommercialRegistrationNo,
            TaxNumber = request.TaxNumber,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = true
        };
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync("Created", tenant.Id, currentUser.UserId, nameof(Tenant), tenant.Id.ToString(), newValues: tenant.CompanyName, cancellationToken: cancellationToken);
        return ApiResult(BaseResponseDto<TenantDto>.Ok(ToDto(tenant), "تم إنشاء الشركة بنجاح."));
    }

    [HttpPut("UpdateTenant/{id:guid}")]
    [HasPermission("Tenants.Update")]
    public async Task<IActionResult> Update(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessTenantAsync(id, cancellationToken))
        {
            return ForbiddenResponse();
        }

        var tenant = await dbContext.Tenants.FindAsync([id], cancellationToken);
        if (tenant is null)
        {
            return ApiResult(BaseResponseDto<TenantDto>.NotFound("الشركة غير موجودة."));
        }

        var oldValue = tenant.CompanyName;
        tenant.CompanyName = request.CompanyName;
        tenant.CompanyNameAr = request.CompanyName;
        tenant.CompanyNameEn = request.CompanyName;
        tenant.CommercialRegistrationNo = request.CommercialRegistrationNo;
        tenant.TaxNumber = request.TaxNumber;
        tenant.Address = request.Address;
        tenant.Phone = request.Phone;
        tenant.Email = request.Email;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync("Updated", tenant.Id, currentUser.UserId, nameof(Tenant), tenant.Id.ToString(), oldValue, tenant.CompanyName, cancellationToken: cancellationToken);
        return ApiResult(BaseResponseDto<TenantDto>.Ok(ToDto(tenant), "تم تحديث بيانات الشركة."));
    }

    [HttpPatch("ActivateTenant/{id:guid}")]
    [HasPermission("Tenants.Activate")]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        return SetActive(id, true, cancellationToken);
    }

    [HttpPatch("DeactivateTenant/{id:guid}")]
    [HasPermission("Tenants.Deactivate")]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        return SetActive(id, false, cancellationToken);
    }

    private async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (!await CanAccessTenantAsync(id, cancellationToken))
        {
            return ForbiddenResponse();
        }

        var tenant = await dbContext.Tenants.FindAsync([id], cancellationToken);
        if (tenant is null)
        {
            return ApiResult(BaseResponseDto<TenantDto>.NotFound("الشركة غير موجودة."));
        }

        tenant.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(isActive ? "Tenant activated" : "Tenant deactivated", tenant.Id, currentUser.UserId, nameof(Tenant), tenant.Id.ToString(), cancellationToken: cancellationToken);
        return ApiResult(BaseResponseDto<TenantDto>.Ok(
            ToDto(tenant),
            isActive ? "تم تفعيل الشركة." : "تم إيقاف الشركة."));
    }

    private async Task<bool> CanAccessTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return currentUser.UserId is { } userId &&
               await tenantAccessService.CanAccessTenantAsync(
                   userId,
                   tenantId,
                   cancellationToken);
    }

    private static TenantDto ToDto(Tenant tenant)
    {
        return new TenantDto(
            tenant.Id,
            tenant.CompanyName,
            tenant.CommercialRegistrationNo,
            tenant.TaxNumber,
            tenant.Address,
            tenant.Phone,
            tenant.Email,
            tenant.IsActive)
        {
            TenantNo = tenant.TenantNo
        };
    }

    private ObjectResult ForbiddenResponse()
    {
        return
        StatusCode(StatusCodes.Status403Forbidden, BaseResponseDto<object>.Fail("ليس لديك صلاحية لتنفيذ هذا الإجراء."));
    }
}
