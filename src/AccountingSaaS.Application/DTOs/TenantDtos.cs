namespace AccountingSaaS.Application.DTOs;

public sealed record TenantDto(
    Guid Id,
    string CompanyName,
    string? CommercialRegistrationNo,
    string? TaxNumber,
    string? Address,
    string? Phone,
    string? Email,
    bool IsActive)
{
    public long TenantNo { get; init; }
}

public sealed record CreateTenantRequest(string CompanyName, string? CommercialRegistrationNo, string? TaxNumber, string? Address, string? Phone, string? Email);
public sealed record UpdateTenantRequest(string CompanyName, string? CommercialRegistrationNo, string? TaxNumber, string? Address, string? Phone, string? Email);
public sealed record ValidateTenantRequest(Guid TenantId);
