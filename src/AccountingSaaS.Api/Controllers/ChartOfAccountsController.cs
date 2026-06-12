using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChartOfAccountsController(IChartOfAccountsService service)
    : AccountingControllerBase
{
    [HttpGet("GetChartOfAccountsByFilter")]
    [HasPermission("ChartOfAccounts.View")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        return ApiResult(
            await service.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("GetChartOfAccountsTree")]
    [HasPermission("ChartOfAccounts.View")]
    public async Task<IActionResult> GetTree(
        CancellationToken cancellationToken)
    {
        return ApiResult(
            await service.GetTreeAsync(cancellationToken));
    }

    [HttpGet("GetChartOfAccountById/{id:guid}")]
    [HasPermission("ChartOfAccounts.View")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ApiResult(
            await service.GetAccountAsync(id, cancellationToken));
    }

    [HttpPost("AddChartOfAccount")]
    [HasPermission("ChartOfAccounts.Create")]
    public async Task<IActionResult> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        return ApiResult(
            await service.CreateAccountAsync(request, cancellationToken));
    }

    [HttpPut("UpdateChartOfAccount/{id:guid}")]
    [HasPermission("ChartOfAccounts.Update")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        return ApiResult(
            await service.UpdateAccountAsync(id, request, cancellationToken));
    }

    [HttpPatch("ActivateChartOfAccount/{id:guid}")]
    [HasPermission("ChartOfAccounts.Activate")]
    public async Task<IActionResult> Activate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ApiResult(
            await service.ActivateAsync(id, cancellationToken));
    }

    [HttpPatch("DeactivateChartOfAccount/{id:guid}")]
    [HasPermission("ChartOfAccounts.Deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ApiResult(
            await service.DeactivateAsync(id, cancellationToken));
    }
}