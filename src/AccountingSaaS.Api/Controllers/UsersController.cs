using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsersController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IAuditLogService auditLogService,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    ITenantAccessService tenantAccessService) : AccountingControllerBase
{
    [HttpGet("GetUsers")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var users = await ApplyUserScope(dbContext.Users.AsQueryable())
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            result.Add(new UserDto(user.Id, user.FullName, user.Email!, user.PhoneNumber, user.TenantId, user.IsActive, (await userManager.GetRolesAsync(user)).ToList()));
        }

        return Ok(BaseResponseDto<IReadOnlyList<UserDto>>.Ok(result));
    }

    [HttpPost("AddUser")]
    [HasPermission("Users.Create")]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var requestedRoles = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!currentUser.IsSuperAdmin && requestedRoles.Contains(Roles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return ForbiddenResponse();
        }

        if (request.TenantId.HasValue && !await CanAssignTenantIdsAsync([request.TenantId.Value], cancellationToken))
        {
            return ForbiddenResponse();
        }

        var tenantId = await ResolveAssignableTenantIdAsync(request.TenantId, requestedRoles, cancellationToken);
        if (tenantId is null && requestedRoles.Any(IsTenantScopedRole))
        {
            return ApiResult(BaseResponseDto<UserDto>.Fail("TenantId is required for tenant users."));
        }

        if (!await CanAssignTenantIdsAsync(request.TenantAccessIds, cancellationToken))
        {
            return ForbiddenResponse();
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            UserName = request.Email,
            PhoneNumber = request.PhoneNumber,
            TenantId = tenantId,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.UserId
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return ApiResult(BaseResponseDto<UserDto>.Fail("User could not be created.", createResult.Errors.Select(x => x.Description)));
        }

        var roleResult = await userManager.AddToRolesAsync(user, requestedRoles);
        if (!roleResult.Succeeded)
        {
            return ApiResult(BaseResponseDto<UserDto>.Fail("Roles could not be assigned.", roleResult.Errors.Select(x => x.Description)));
        }

        foreach (var accessTenantId in request.TenantAccessIds.Distinct())
        {
            dbContext.UserTenantAccesses.Add(new UserTenantAccess { UserId = user.Id, TenantId = accessTenantId });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync("User created", user.TenantId, currentUser.UserId, nameof(ApplicationUser), user.Id.ToString(), newValues: user.Email, cancellationToken: cancellationToken);
        return Ok(BaseResponseDto<UserDto>.Ok(new UserDto(user.Id, user.FullName, user.Email!, user.PhoneNumber, user.TenantId, user.IsActive, requestedRoles), "User created."));
    }

    [HttpPut("UpdateUser/{id:guid}")]
    [HasPermission("Users.Update")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return ApiResult(BaseResponseDto<UserDto>.Fail("User was not found."));
        }

        if (!await CanManageUserAsync(user, cancellationToken))
        {
            return ForbiddenResponse();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (request.TenantId.HasValue && !await CanAssignTenantIdsAsync([request.TenantId.Value], cancellationToken))
        {
            return ForbiddenResponse();
        }

        var tenantId = await ResolveAssignableTenantIdAsync(request.TenantId, roles, cancellationToken);
        if (tenantId is null && roles.Any(IsTenantScopedRole))
        {
            return ApiResult(BaseResponseDto<UserDto>.Fail("TenantId is required for tenant users."));
        }

        var oldValue = $"{user.FullName}|{user.PhoneNumber}|{user.TenantId}|{user.IsActive}";
        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;
        user.TenantId = tenantId;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedByUserId = currentUser.UserId;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return ApiResult(BaseResponseDto<UserDto>.Fail("User could not be updated.", result.Errors.Select(x => x.Description)));
        }

        await auditLogService.LogAsync("User updated", user.TenantId, currentUser.UserId, nameof(ApplicationUser), user.Id.ToString(), oldValue, $"{user.FullName}|{user.PhoneNumber}|{user.TenantId}|{user.IsActive}", cancellationToken: cancellationToken);
        return Ok(BaseResponseDto<UserDto>.Ok(new UserDto(user.Id, user.FullName, user.Email!, user.PhoneNumber, user.TenantId, user.IsActive, (await userManager.GetRolesAsync(user)).ToList()), "User updated."));
    }

    [HttpPost("AssignRoles/{id:guid}")]
    [HasPermission("Users.AssignRoles")]
    public async Task<IActionResult> AssignRoles(Guid id, AssignRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return ApiResult(BaseResponseDto<object>.Fail("User was not found."));
        }

        if (!await CanManageUserAsync(user, cancellationToken))
        {
            return ForbiddenResponse();
        }

        if (!currentUser.IsSuperAdmin && request.Roles.Contains(Roles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return ForbiddenResponse();
        }

        var existing = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, existing);
        var newRoles = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (newRoles.Any(IsTenantScopedRole) && user.TenantId is null)
        {
            return ApiResult(BaseResponseDto<object>.Fail("TenantId is required before assigning tenant-scoped roles."));
        }

        var result = await userManager.AddToRolesAsync(user, newRoles);
        if (!result.Succeeded)
        {
            return ApiResult(BaseResponseDto<object>.Fail("Roles could not be assigned.", result.Errors.Select(x => x.Description)));
        }

        await auditLogService.LogAsync("Roles assigned", user.TenantId, currentUser.UserId, nameof(ApplicationUser), user.Id.ToString(), string.Join(",", existing), string.Join(",", newRoles), cancellationToken: cancellationToken);
        return Ok(BaseResponseDto<object>.Ok(null, "Roles assigned."));
    }

    [HttpPost("AssignTenantAccess/{id:guid}")]
    [HasPermission("Users.Update")]
    public async Task<IActionResult> AssignTenantAccess(Guid id, AssignTenantAccessRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return ApiResult(BaseResponseDto<object>.Fail("User was not found."));
        }

        if (!await CanManageUserAsync(user, cancellationToken) || !await CanAssignTenantIdsAsync(request.TenantIds, cancellationToken))
        {
            return ForbiddenResponse();
        }

        dbContext.UserTenantAccesses.RemoveRange(dbContext.UserTenantAccesses.Where(x => x.UserId == id));
        foreach (var tenantId in request.TenantIds.Distinct())
        {
            dbContext.UserTenantAccesses.Add(new UserTenantAccess { UserId = id, TenantId = tenantId });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync("Tenant access assigned", userId: currentUser.UserId, entityName: nameof(UserTenantAccess), entityId: id.ToString(), newValues: string.Join(",", request.TenantIds), cancellationToken: cancellationToken);
        return Ok(BaseResponseDto<object>.Ok(null, "Tenant access assigned."));
    }

    private IQueryable<ApplicationUser> ApplyUserScope(IQueryable<ApplicationUser> query)
    {
        if (currentUser.IsSuperAdmin)
        {
            return query;
        }

        if (currentUser.UserId is not { } userId)
        {
            return query.Where(_ => false);
        }

        if (currentUser.IsAccountingOfficeAdmin || currentUser.Roles.Contains(Roles.Accountant, StringComparer.OrdinalIgnoreCase))
        {
            return query.Where(user =>
                user.Id == userId
                || (user.TenantId.HasValue && dbContext.UserTenantAccesses.Any(access => access.UserId == userId && access.TenantId == user.TenantId.Value))
                || dbContext.UserTenantAccesses.Any(targetAccess =>
                    targetAccess.UserId == user.Id
                    && dbContext.UserTenantAccesses.Any(myAccess => myAccess.UserId == userId && myAccess.TenantId == targetAccess.TenantId)));
        }

        return query.Where(user => user.TenantId == currentTenant.TenantId);
    }

    private async Task<bool> CanManageUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return true;
        }

        if (currentUser.UserId == user.Id)
        {
            return true;
        }

        if (currentUser.UserId is not { } userId)
        {
            return false;
        }

        if (user.TenantId.HasValue && await tenantAccessService.CanAccessTenantAsync(userId, user.TenantId.Value, cancellationToken))
        {
            return true;
        }

        return await dbContext.UserTenantAccesses.AnyAsync(targetAccess =>
            targetAccess.UserId == user.Id
            && dbContext.UserTenantAccesses.Any(myAccess => myAccess.UserId == userId && myAccess.TenantId == targetAccess.TenantId), cancellationToken);
    }

    private async Task<Guid?> ResolveAssignableTenantIdAsync(Guid? requestedTenantId, IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return requestedTenantId;
        }

        if (requestedTenantId is not { } tenantId)
        {
            return currentTenant.TenantId;
        }

        if (currentUser.UserId is { } userId && await tenantAccessService.CanAccessTenantAsync(userId, tenantId, cancellationToken))
        {
            return tenantId;
        }

        return roles.Any(IsTenantScopedRole) ? null : currentTenant.TenantId;
    }

    private async Task<bool> CanAssignTenantIdsAsync(IEnumerable<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var distinctTenantIds = tenantIds.Distinct().ToList();
        if (currentUser.IsSuperAdmin)
        {
            return distinctTenantIds.Count == 0
                || await dbContext.Tenants.CountAsync(x => distinctTenantIds.Contains(x.Id) && x.IsActive, cancellationToken) == distinctTenantIds.Count;
        }

        if (currentUser.UserId is not { } userId)
        {
            return false;
        }

        foreach (var tenantId in distinctTenantIds)
        {
            if (!await tenantAccessService.CanAccessTenantAsync(userId, tenantId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTenantScopedRole(string role) =>
        role.Equals(Roles.Accountant, StringComparison.OrdinalIgnoreCase)
        || role.Equals(Roles.Reviewer, StringComparison.OrdinalIgnoreCase)
        || role.Equals(Roles.CompanyOwner, StringComparison.OrdinalIgnoreCase)
        || role.Equals(Roles.CompanyUser, StringComparison.OrdinalIgnoreCase);

    private ObjectResult ForbiddenResponse() =>
        StatusCode(StatusCodes.Status403Forbidden, BaseResponseDto<object>.Fail("Forbidden.", ["You are not allowed to perform this action."]));
}
