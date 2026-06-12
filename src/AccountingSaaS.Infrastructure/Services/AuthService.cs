using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext,
    IConfiguration configuration,
    ICurrentUserService currentUser,
    IAuditLogService auditLogService)
    : IAuthService
{
    public async Task<BaseResponseDto<AuthResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.IsDeleted || !user.IsActive)
        {
            await auditLogService.LogAsync("Failed login", ipAddress: ipAddress, userAgent: userAgent, cancellationToken: cancellationToken);
            return BaseResponseDto<AuthResponse>.Fail("Invalid credentials.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            await auditLogService.LogAsync("Failed login", userId: user.Id, ipAddress: ipAddress, userAgent: userAgent, cancellationToken: cancellationToken);
            return BaseResponseDto<AuthResponse>.Fail("Invalid credentials.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        var response = await CreateAuthResponseAsync(user, ipAddress, cancellationToken);
        await auditLogService.LogAsync("Login", user.TenantId, user.Id, ipAddress: ipAddress, userAgent: userAgent, cancellationToken: cancellationToken);
        return BaseResponseDto<AuthResponse>.Ok(response, "Login successful.");
    }

    public async Task<BaseResponseDto<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (storedToken is null)
        {
            await auditLogService.LogAsync("Failed refresh token", ipAddress: ipAddress, userAgent: userAgent, cancellationToken: cancellationToken);
            return BaseResponseDto<AuthResponse>.Fail("Invalid refresh token.");
        }

        if (!storedToken.IsActive || storedToken.User.IsDeleted || !storedToken.User.IsActive)
        {
            if (storedToken.RevokedAt.HasValue)
            {
                await RevokeDescendantRefreshTokensAsync(storedToken, ipAddress, cancellationToken);
            }

            await auditLogService.LogAsync("Failed refresh token", storedToken.User.TenantId, storedToken.UserId, ipAddress: ipAddress, userAgent: userAgent, cancellationToken: cancellationToken);
            return BaseResponseDto<AuthResponse>.Fail("Invalid refresh token.");
        }

        var newRefreshToken = GenerateRefreshToken();
        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.ReplacedByTokenHash = HashToken(newRefreshToken);

        var auth = await CreateAuthResponseAsync(storedToken.User, ipAddress, cancellationToken, newRefreshToken);
        await auditLogService.LogAsync("Refresh token", storedToken.User.TenantId, storedToken.User.Id, ipAddress: ipAddress, userAgent: userAgent, cancellationToken: cancellationToken);
        return BaseResponseDto<AuthResponse>.Ok(auth, "Token refreshed.");
    }

    private async Task RevokeDescendantRefreshTokensAsync(RefreshToken refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var replacementHash = refreshToken.ReplacedByTokenHash;
        while (!string.IsNullOrWhiteSpace(replacementHash))
        {
            var childToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == replacementHash, cancellationToken);
            if (childToken is null)
            {
                return;
            }

            if (childToken.RevokedAt is null)
            {
                childToken.RevokedAt = DateTimeOffset.UtcNow;
                childToken.RevokedByIp = ipAddress;
            }

            replacementHash = childToken.ReplacedByTokenHash;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<BaseResponseDto<object>> LogoutAsync(LogoutRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (storedToken is not null && storedToken.RevokedAt is null)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            storedToken.RevokedByIp = ipAddress;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.LogAsync("Logout", userId: storedToken.UserId, ipAddress: ipAddress, userAgent: userAgent, cancellationToken: cancellationToken);
        }

        return BaseResponseDto<object>.Ok(null, "Logged out.");
    }

    public async Task<BaseResponseDto<CurrentUserDto>> MeAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return BaseResponseDto<CurrentUserDto>.Fail("User is not authenticated.");
        }

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return BaseResponseDto<CurrentUserDto>.Fail("User was not found.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsAsync(roles, cancellationToken);
        return BaseResponseDto<CurrentUserDto>.Ok(new CurrentUserDto(user.Id, user.FullName, user.Email!, roles.ToList(), permissions, user.TenantId));
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken, string? refreshToken = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsAsync(roles, cancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:AccessTokenMinutes", 15));
        var accessToken = CreateAccessToken(user, roles, permissions, expiresAt);
        refreshToken ??= GenerateRefreshToken();
        dbContext.RefreshTokens.Add(CreateRefreshToken(user.Id, refreshToken, ipAddress));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AuthResponse(accessToken, refreshToken, expiresAt, new CurrentUserDto(user.Id, user.FullName, user.Email!, roles.ToList(), permissions, user.TenantId));
    }

    private string CreateAccessToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<string> permissions, DateTimeOffset expiresAt)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is missing.")));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<IReadOnlyList<string>> GetPermissionsAsync(IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        var roleNames = roles.ToList();
        if (roleNames.Contains(Roles.SuperAdmin))
        {
            return Permissions.All;
        }

        return await dbContext.RolePermissions
            .Where(x => roleNames.Contains(x.Role.Name!))
            .Select(x => x.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private RefreshToken CreateRefreshToken(Guid userId, string token, string? ipAddress) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = HashToken(token),
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(configuration.GetValue<int>("Jwt:RefreshTokenDays", 7)),
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedByIp = ipAddress
    };

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
