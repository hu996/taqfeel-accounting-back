using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class JournalEntriesController(IJournalEntryService service) : AccountingControllerBase
{
    [HttpGet("GetJournalEntriesByFilter")]
    [HasPermission("JournalEntries.View")]
    public async Task<IActionResult> GetPaged([FromQuery] AccountingPagedRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetJournalEntryById/{id:guid}")]
    [HasPermission("JournalEntries.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("AddJournalEntry")]
    [HasPermission("JournalEntries.Create")]
    public async Task<IActionResult> CreateDraft(CreateJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateDraftAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPut("UpdateJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Update")]
    public async Task<IActionResult> UpdateDraft(Guid id, UpdateJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateDraftAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("PostJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Post")]
    public async Task<IActionResult> Post(Guid id, PostJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.PostAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("ReverseJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Reverse")]
    public async Task<IActionResult> Reverse(Guid id, ReverseJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ReverseAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("CancelJournalEntry/{id:guid}")]
    [HasPermission("JournalEntries.Cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(id, cancellationToken);
        return ApiResult(result);
    }
}