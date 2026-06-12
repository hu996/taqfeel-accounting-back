namespace AccountingSaaS.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
