//using AccountingSaaS.Application.Authorization;
//using AccountingSaaS.Application.DTOs;
//using AccountingSaaS.Application.Interfaces;
//using AccountingSaaS.Infrastructure.Persistence;
//using AccountingSaaS.Shared.Responses;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace AccountingSaaS.Api.Controllers;

//[ApiController]
//[Route("api/audit-logs")]
//public sealed class AuditLogsController(AppDbContext dbContext, ICurrentUserService currentUser, ICurrentTenantService currentTenant) : ControllerBase
//{
//    [HttpGet("GetAuditLogsByFilter")]
//    [HasPermission("AuditLogs.View")]
//    public async Task<IActionResult> Get([FromQuery] AuditLogFilter filter, CancellationToken cancellationToken)
//    {
//        var query = dbContext.AuditLogs.AsNoTracking();
//        if (!currentUser.IsSuperAdmin)
//        {
//            if (!currentTenant.IsTenantSelected)
//            {
//                return BadRequest(BaseResponseDto<IReadOnlyList<AuditLogDto>>.Fail("Tenant context is required."));
//            }

//            if (filter.TenantId.HasValue && filter.TenantId != currentTenant.TenantId)
//            {
//                return StatusCode(StatusCodes.Status403Forbidden, BaseResponseDto<object>.Fail("Forbidden.", ["You are not allowed to view another tenant's audit logs."]));
//            }

//            query = query.Where(x => x.TenantId == currentTenant.TenantId);
//        }

//        if (filter.TenantId.HasValue) query = query.Where(x => x.TenantId == filter.TenantId);
//        if (filter.UserId.HasValue) query = query.Where(x => x.UserId == filter.UserId);
//        if (!string.IsNullOrWhiteSpace(filter.Action)) query = query.Where(x => x.Action == filter.Action);
//        if (filter.DateFrom.HasValue) query = query.Where(x => x.CreatedAt >= filter.DateFrom);
//        if (filter.DateTo.HasValue) query = query.Where(x => x.CreatedAt <= filter.DateTo);

//        var logs = await query.OrderByDescending(x => x.CreatedAt).Take(200).Select(x => new AuditLogDto(
//            x.Id, x.TenantId, x.UserId, x.Action, x.EntityName, x.EntityId, x.OldValues, x.NewValues, x.IpAddress, x.UserAgent, x.CreatedAt)).ToListAsync(cancellationToken);

//        return Ok(BaseResponseDto<IReadOnlyList<AuditLogDto>>.Ok(logs));
//    }
//}
