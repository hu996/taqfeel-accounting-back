using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FinancialYearsController(IFinancialYearService service) : AccountingControllerBase
{
    [HttpGet("GetFinancialYearsByFilter")]
    [HasPermission("FinancialYears.View")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetFinancialYearById/{id:guid}")]
    [HasPermission("FinancialYears.View")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("AddFinancialYear")]
    [HasPermission("FinancialYears.Create")]
    public async Task<IActionResult> Create(
        CreateFinancialYearRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return ApiResult(result);
    }

}
