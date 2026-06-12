using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AccountingPeriodsController(IAccountingPeriodService service) : AccountingControllerBase
{
    [HttpGet("GetAccountingPeriodsByFilter")]
    [HasPermission("AccountingPeriods.View")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(request, cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetAccountingPeriodById/{id:guid}")]
    [HasPermission("AccountingPeriods.View")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);

        return ApiResult(result);
    }

    [HttpPost("AddAccountingPeriod")]
    [HasPermission("AccountingPeriods.Create")]
    public async Task<IActionResult> Create(
        CreateAccountingPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);

        return ApiResult(result);
    }

    [HttpPut("UpdateAccountingPeriod/{id:guid}")]
    [HasPermission("AccountingPeriods.Update")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateAccountingPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);

        return ApiResult(result);
    }

    [HttpPost("LockAccountingPeriod/{id:guid}")]
    [HasPermission("AccountingPeriods.Lock")]
    public async Task<IActionResult> Lock(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.LockPeriodAsync(id, cancellationToken);

        return ApiResult(result);
    }

    [HttpPost("SubmitForReview/{id:guid}")]
    [HasPermission("AccountingPeriods.Update")]
    public async Task<IActionResult> SubmitForReview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitForReviewAsync(id, cancellationToken);

        return ApiResult(result);
    }

    [HttpPost("CloseAccountingPeriod/{id:guid}")]
    [HasPermission("AccountingPeriods.Close")]
    public async Task<IActionResult> Close(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.ClosePeriodAsync(id, cancellationToken);

        return ApiResult(result);
    }

    [HttpPost("ReopenAccountingPeriod/{id:guid}")]
    [HasPermission("AccountingPeriods.Reopen")]
    public async Task<IActionResult> Reopen(
        Guid id,
        ReopenPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ReopenPeriodAsync(id, request, cancellationToken);

        return ApiResult(result);
    }
}