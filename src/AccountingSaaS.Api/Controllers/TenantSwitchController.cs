using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TenantSwitchController(ITenantAccessService tenantAccessService, IAuditLogService auditLogService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("GetMyTenants")]
    public async Task<IActionResult> MyTenants(CancellationToken cancellationToken)
    {
        var tenants = await tenantAccessService.GetAccessibleTenantsAsync(cancellationToken);
        return Ok(BaseResponseDto<IReadOnlyList<TenantDto>>.Ok(tenants));
    }

    [HttpPost("ValidateTenantAccess")]
    public async Task<IActionResult> Validate(ValidateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessService.ValidateTenantAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, BaseResponseDto<object>.Fail("Forbidden.", ["You are not allowed to access this tenant."]));
        }

        await auditLogService.LogAsync("Tenant switch validation", request.TenantId, currentUser.UserId, cancellationToken: cancellationToken);
        return Ok(BaseResponseDto<TenantDto>.Ok(tenant, "Tenant access validated."));
    }
}
