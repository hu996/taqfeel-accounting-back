using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.Interfaces;

public interface IAuthService
{
    Task<BaseResponseDto<AuthResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<BaseResponseDto<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<BaseResponseDto<object>> LogoutAsync(LogoutRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<BaseResponseDto<CurrentUserDto>> MeAsync(CancellationToken cancellationToken);
}

public interface ITenantAccessService
{
    Task<bool> CanAccessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantDto>> GetAccessibleTenantsAsync(CancellationToken cancellationToken);
    Task<TenantDto?> ValidateTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface IAuditLogService
{
    Task LogAsync(string action, Guid? tenantId = null, Guid? userId = null, string? entityName = null, string? entityId = null, string? oldValues = null, string? newValues = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
}
