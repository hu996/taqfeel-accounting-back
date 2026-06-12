using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ClosingChecklistController(IClosingChecklistService service) : AccountingControllerBase
{
    [HttpPost("AddTemplate")]
    [HasPermission("ClosingChecklist.Manage")]
    public async Task<IActionResult> CreateTemplate(
        CreateClosingChecklistTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateTemplateAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("AddDefaultTemplate")]
    [HasPermission("ClosingChecklist.Manage")]
    public async Task<IActionResult> CreateDefaultTemplate(
        CancellationToken cancellationToken)
    {
        var result = await service.CreateDefaultTemplateAsync(cancellationToken);
        return ApiResult(result);
    }

    [HttpPut("UpdateTemplate/{id:guid}")]
    [HasPermission("ClosingChecklist.Manage")]
    public async Task<IActionResult> UpdateTemplate(
        Guid id,
        UpdateClosingChecklistTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateTemplateAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("AddTemplateItem")]
    [HasPermission("ClosingChecklist.Manage")]
    public async Task<IActionResult> AddItem(
        CreateClosingChecklistTemplateItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AddTemplateItemAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPut("UpdateTemplateItem/{id:guid}")]
    [HasPermission("ClosingChecklist.Manage")]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        UpdateClosingChecklistTemplateItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateTemplateItemAsync(id, request, cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("GenerateClosingTasks")]
    [HasPermission("ClosingChecklist.Manage")]
    public async Task<IActionResult> GenerateTasks(
        GenerateClosingTasksRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GenerateTasksForPeriodAsync(request, cancellationToken);
        return ApiResult(result);
    }
}