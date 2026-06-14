using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Persistence;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class AuditLogService(AppDbContext dbContext) : IAuditLogService
{
    public async Task LogAsync(string action, Guid? tenantId = null, Guid? userId = null, string? entityName = null, string? entityId = null, string? oldValues = null, string? newValues = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            TenantId = tenantId,
            UserId = userId,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            UserAgent = userAgent,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
