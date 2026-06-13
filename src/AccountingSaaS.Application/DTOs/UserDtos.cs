namespace AccountingSaaS.Application.DTOs;

public sealed record UserDto(Guid Id, string FullName, string Email, string? PhoneNumber, Guid? TenantId, bool IsActive, IReadOnlyList<string> Roles)
{
    public long UserNo { get; init; }
}

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber,
    Guid? TenantId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> TenantAccessIds);

public sealed record UpdateUserRequest(string FullName, string? PhoneNumber, Guid? TenantId, bool IsActive);
public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);
public sealed record AssignTenantAccessRequest(IReadOnlyList<Guid> TenantIds);
