namespace AccountingSaaS.Application.DTOs;

public sealed class LookupDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? LabelAr { get; set; }
    public string? LabelEn { get; set; }
    public string? Code { get; set; }
    public string? Extra { get; set; }
    public bool IsActive { get; set; }
}

public sealed class LookupRequest
{
    public string? Search { get; set; }
    public int Take { get; set; } = 50;
    public bool ActiveOnly { get; set; } = true;
    public Guid? FinancialYearId { get; set; }
    public Guid? AccountingPeriodId { get; set; }
    public Guid? ParentId { get; set; }
    public string? Type { get; set; }
}
