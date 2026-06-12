namespace AccountingSaaS.Application.DTOs;

public sealed record AuditLogDto(
    Guid Id,
    Guid? TenantId,
    Guid? UserId,
    string Action,
    string? EntityName,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);

public sealed class AuditLogFilter
{
    public Guid? TenantId { get; init; }
    public Guid? UserId { get; init; }
    public string? Action { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
}
