namespace AccountingSaaS.Application.DTOs;

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

public sealed record CurrentUserDto(
    Guid Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    Guid? TenantId);
