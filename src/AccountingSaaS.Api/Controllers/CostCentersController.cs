using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CostCentersController(ICostCenterService service) : AccountingControllerBase
{
    [HttpGet("GetCostCentersByFilter")]
    [HasPermission("CostCenters.View")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("AddCostCenter")]
    [HasPermission("CostCenters.Create")]
    public async Task<IActionResult> Create(
        CreateCostCenterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPut("UpdateCostCenter/{id:guid}")]
    [HasPermission("CostCenters.Update")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCostCenterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPatch("ActivateCostCenter/{id:guid}")]
    [HasPermission("CostCenters.Update")]
    public async Task<IActionResult> Activate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.ActivateAsync(id, cancellationToken);
        return ApiResult(result);
    }

    [HttpPatch("DeactivateCostCenter/{id:guid}")]
    [HasPermission("CostCenters.Update")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateAsync(id, cancellationToken);
        return ApiResult(result);
    }
}