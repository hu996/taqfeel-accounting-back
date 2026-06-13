using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TenantSwitchController(ITenantAccessService tenantAccessService, IAuditLogService auditLogService, ICurrentUserService currentUser) : AccountingControllerBase
{
    [HttpGet("GetMyTenants")]
    public async Task<IActionResult> MyTenants(CancellationToken cancellationToken)
    {
        var tenants = await tenantAccessService.GetAccessibleTenantsAsync(cancellationToken);
        return ApiResult(BaseResponseDto<IReadOnlyList<TenantDto>>.Ok(tenants));
    }

    [HttpPost("ValidateTenantAccess")]
    public async Task<IActionResult> Validate(ValidateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessService.ValidateTenantAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, BaseResponseDto<object>.Fail("ليس لديك صلاحية للوصول إلى هذه الشركة."));
        }

        await auditLogService.LogAsync("Tenant switch validation", request.TenantId, currentUser.UserId, cancellationToken: cancellationToken);
        return ApiResult(BaseResponseDto<TenantDto>.Ok(tenant, "تم التحقق من صلاحية الوصول إلى الشركة."));
    }
}
