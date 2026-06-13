namespace AccountingSaaS.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public long TenantNo { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CommercialRegistrationNo { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
