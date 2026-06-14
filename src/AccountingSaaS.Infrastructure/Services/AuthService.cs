using System.Security.Cryptography;
using System.Text;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly AppDbContext dbContext;
    private readonly IConfiguration configuration;
    private readonly ICurrentUserService currentUser;
    private readonly IAuditLogService auditLogService;
    private readonly ISessionContextFactory sessionContextFactory;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IConfiguration configuration,
        ICurrentUserService currentUser,
        IAuditLogService auditLogService,
        ISessionContextFactory sessionContextFactory)
    {
        this.userManager = userManager;
        this.dbContext = dbContext;
        this.configuration = configuration;
        this.currentUser = currentUser;
        this.auditLogService = auditLogService;
        this.sessionContextFactory = sessionContextFactory;
    }

    public async Task<BaseResponseDto<LoginResponseDto>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            user = await userManager.FindByNameAsync(request.Email);
        }

        if (user is null || user.IsDeleted || !user.IsActive)
        {
            await auditLogService.LogAsync(
                "Failed login",
                userAgent: userAgent,
                cancellationToken: cancellationToken);
            return BaseResponseDto<LoginResponseDto>.Fail("Invalid credentials.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await auditLogService.LogAsync(
                "Failed login - locked account",
                userId: user.Id,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
            return BaseResponseDto<LoginResponseDto>.Fail("Account is temporarily locked.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            await auditLogService.LogAsync(
                "Failed login",
                userId: user.Id,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
            return BaseResponseDto<LoginResponseDto>.Fail("Invalid credentials.");
        }

        LoginResponseDto response;
        try
        {
            response = await sessionContextFactory.CreateAsync(
                user.Id,
                user.ActiveFinancialYearId,
                cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            return BaseResponseDto<LoginResponseDto>.Fail(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BaseResponseDto<LoginResponseDto>.Fail(exception.Message);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.ActiveFinancialYearId = response.ActiveFinancialYearId;
        await userManager.UpdateAsync(user);

        var refreshToken = GenerateRefreshToken();
        response.RefreshToken = refreshToken;
        dbContext.RefreshTokens.Add(CreateRefreshToken(user.Id, refreshToken, ipAddress));
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "Login",
            response.TenantId,
            user.Id,
            userAgent: userAgent,
            cancellationToken: cancellationToken);
        return BaseResponseDto<LoginResponseDto>.Ok(response, "Login successful.");
    }

    public async Task<BaseResponseDto<LoginResponseDto>> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (storedToken is null)
        {
            await auditLogService.LogAsync(
                "Failed refresh token",
                userAgent: userAgent,
                cancellationToken: cancellationToken);
            return BaseResponseDto<LoginResponseDto>.Fail("Invalid refresh token.");
        }

        if (!storedToken.IsActive ||
            storedToken.User.IsDeleted ||
            !storedToken.User.IsActive)
        {
            if (storedToken.RevokedAt.HasValue)
            {
                await RevokeDescendantRefreshTokensAsync(
                    storedToken,
                    ipAddress,
                    cancellationToken);
            }

            await auditLogService.LogAsync(
                "Failed refresh token",
                storedToken.User.TenantId,
                storedToken.UserId,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
            return BaseResponseDto<LoginResponseDto>.Fail("Invalid refresh token.");
        }

        LoginResponseDto response;
        try
        {
            response = await sessionContextFactory.CreateAsync(
                storedToken.User.Id,
                storedToken.User.ActiveFinancialYearId,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or InvalidOperationException)
        {
            return BaseResponseDto<LoginResponseDto>.Fail(exception.Message);
        }

        var newRefreshToken = GenerateRefreshToken();
        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.ReplacedByTokenHash = HashToken(newRefreshToken);
        response.RefreshToken = newRefreshToken;
        dbContext.RefreshTokens.Add(
            CreateRefreshToken(storedToken.UserId, newRefreshToken, ipAddress));
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "Refresh token",
            response.TenantId,
            storedToken.User.Id,
            userAgent: userAgent,
            cancellationToken: cancellationToken);
        return BaseResponseDto<LoginResponseDto>.Ok(response, "Token refreshed.");
    }

    public async Task<BaseResponseDto<object>> LogoutAsync(
        LogoutRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                item => item.TokenHash == tokenHash,
                cancellationToken);
        if (storedToken is not null && storedToken.RevokedAt is null)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            storedToken.RevokedByIp = ipAddress;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.LogAsync(
                "Logout",
                userId: storedToken.UserId,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
        }

        return BaseResponseDto<object>.Ok(null, "Logged out.");
    }

    public async Task<BaseResponseDto<object>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return BaseResponseDto<object>.Fail("User is not authenticated.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.PasswordHash is null)
        {
            return BaseResponseDto<object>.NotFound("User was not found.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return BaseResponseDto<object>.Fail("Current password is incorrect.");
        }

        var recentHashes = await dbContext.PasswordHistories
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.PasswordHash)
            .Take(2)
            .ToListAsync(cancellationToken);
        recentHashes.Insert(0, user.PasswordHash);
        var hasher = userManager.PasswordHasher;
        var passwordWasUsed = recentHashes
            .Distinct()
            .Any(hash =>
                hasher.VerifyHashedPassword(user, hash, request.NewPassword) !=
                PasswordVerificationResult.Failed);
        if (passwordWasUsed)
        {
            return BaseResponseDto<object>.Fail(
                "The new password cannot match any of the last three passwords.");
        }

        var oldHash = user.PasswordHash;
        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        if (!result.Succeeded)
        {
            return BaseResponseDto<object>.Fail(
                "Password could not be changed.",
                result.Errors.Select(error => error.Description));
        }

        dbContext.PasswordHistories.Add(new PasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PasswordHash = oldHash,
            CreatedAt = DateTimeOffset.UtcNow
        });
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.MustChangePassword = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return BaseResponseDto<object>.Ok(null, "Password changed successfully.");
    }

    private async Task RevokeDescendantRefreshTokensAsync(
        RefreshToken refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var replacementHash = refreshToken.ReplacedByTokenHash;
        while (!string.IsNullOrWhiteSpace(replacementHash))
        {
            var childToken = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(
                    item => item.TokenHash == replacementHash,
                    cancellationToken);
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

    private RefreshToken CreateRefreshToken(
        Guid userId,
        string token,
        string? ipAddress)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(token),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(
                configuration.GetValue<int>("Jwt:RefreshTokenDays", 7)),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = ipAddress
        };
    }

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
