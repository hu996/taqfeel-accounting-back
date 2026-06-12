using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ClosingSubmissionsController(IClosingSubmissionService service) : AccountingControllerBase
{
    [HttpGet("GetSubmissionsByPeriod/{accountingPeriodId:guid}")]
    [HasPermission("ClosingSubmissions.View")]
    public async Task<IActionResult> ByPeriod(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByPeriodAsync(accountingPeriodId, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("Submit/{accountingPeriodId:guid}")]
    [HasPermission("ClosingSubmissions.Submit")]
    public async Task<IActionResult> Submit(
        Guid accountingPeriodId,
        SubmitClosingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitClosingAsync(
            accountingPeriodId,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpPost("StartReview/{accountingPeriodId:guid}")]
    [HasPermission("ClosingSubmissions.Review")]
    public async Task<IActionResult> StartReview(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await service.StartReviewAsync(accountingPeriodId, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("Approve/{accountingPeriodId:guid}")]
    [HasPermission("ClosingSubmissions.Approve")]
    public async Task<IActionResult> Approve(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveClosingAsync(accountingPeriodId, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("Reject/{accountingPeriodId:guid}")]
    [HasPermission("ClosingSubmissions.Reject")]
    public async Task<IActionResult> Reject(
        Guid accountingPeriodId,
        RejectClosingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectClosingAsync(
            accountingPeriodId,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpPost("ClosePeriod/{accountingPeriodId:guid}")]
    [HasPermission("ClosingSubmissions.ClosePeriod")]
    public async Task<IActionResult> ClosePeriod(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await service.ClosePeriodAsync(accountingPeriodId, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("ReopenPeriod/{accountingPeriodId:guid}")]
    [HasPermission("ClosingSubmissions.ReopenPeriod")]
    public async Task<IActionResult> ReopenPeriod(
        Guid accountingPeriodId,
        ReopenClosingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ReopenPeriodAsync(
            accountingPeriodId,
            request,
            cancellationToken);

        return ApiResult(result);
    }
}