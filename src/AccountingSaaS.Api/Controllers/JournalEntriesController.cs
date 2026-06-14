using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireModule("CoreAccounting")]
public sealed class JournalEntriesController : AccountingControllerBase
{
    private readonly IJournalEntryService service;

    public JournalEntriesController(IJournalEntryService service)
    {
        this.service = service;
    }

    [HttpGet("GetJournalEntriesByFilter")]
    [HasPermission("JournalEntries.View")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetJournalEntryById/{id:guid}")]
    [HasPermission("JournalEntries.View")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("AddJournalEntry")]
    [HasPermission("JournalEntries.Create")]
    public async Task<IActionResult> CreateDraft(
        CreateJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateDraftAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPut("UpdateJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Update")]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        UpdateJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateDraftAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("PostJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Post")]
    public async Task<IActionResult> Post(
        Guid id,
        PostJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.PostAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("ReverseJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Reverse")]
    public async Task<IActionResult> Reverse(
        Guid id,
        ReverseJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ReverseAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("CancelJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(id, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("SubmitForReview/{id:guid}")]
    [HasPermission("JournalEntries.Submit")]
    public async Task<IActionResult> SubmitForReview(
        Guid id,
        SubmitJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitForReviewAsync(
            id,
            request,
            cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("StartReview/{id:guid}")]
    [HasPermission("JournalEntries.Review")]
    public async Task<IActionResult> StartReview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.StartReviewAsync(id, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("Approve/{id:guid}")]
    [HasPermission("JournalEntries.Approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(id, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("Reject/{id:guid}")]
    [HasPermission("JournalEntries.Reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        ReviewJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("ReturnForCorrection/{id:guid}")]
    [HasPermission("JournalEntries.ReturnForCorrection")]
    public async Task<IActionResult> ReturnForCorrection(
        Guid id,
        ReviewJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ReturnForCorrectionAsync(
            id,
            request,
            cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetMyReviewQueue")]
    [HasPermission("JournalEntries.Review")]
    public async Task<IActionResult> GetMyReviewQueue(
        [FromQuery] AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetMyReviewQueueAsync(
            request,
            cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetMyCompanyData")]
    [HasPermission("JournalEntries.View")]
    public async Task<IActionResult> GetMyCompanyData(
        [FromQuery] AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetVersions/{id:guid}")]
    [HasPermission("JournalEntries.View")]
    public async Task<IActionResult> GetVersions(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetVersionsAsync(id, cancellationToken);
        return ApiResult(result);
    }
}
