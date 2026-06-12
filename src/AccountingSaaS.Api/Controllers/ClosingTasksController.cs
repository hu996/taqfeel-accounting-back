using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ClosingTasksController(IClosingTaskService service) : AccountingControllerBase
{
    [HttpGet("GetTasksByPeriod/{accountingPeriodId:guid}")]
    [HasPermission("ClosingTasks.View")]
    public async Task<IActionResult> ByPeriod(Guid accountingPeriodId, CancellationToken cancellationToken)
    {
        return ApiResult(await service.GetTasksByPeriodAsync(accountingPeriodId, cancellationToken));
    }

    [HttpPost("AssignTask/{id:guid}")]
    [HasPermission("ClosingTasks.Manage")]
    public async Task<IActionResult> Assign(Guid id, AssignClosingTaskRequest request, CancellationToken cancellationToken)
    {
        return ApiResult(await service.AssignTaskAsync(id, request, cancellationToken));
    }

    [HttpPost("StartTask/{id:guid}")]
    [HasPermission("ClosingTasks.Manage")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        return ApiResult(await service.StartTaskAsync(id, cancellationToken));
    }

    [HttpPost("SubmitTask/{id:guid}")]
    [HasPermission("ClosingTasks.Submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        return ApiResult(await service.SubmitTaskAsync(id, cancellationToken));
    }

    [HttpPost("ApproveTask/{id:guid}")]
    [HasPermission("ClosingTasks.Approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        return ApiResult(await service.ApproveTaskAsync(id, cancellationToken));
    }

    [HttpPost("RejectTask/{id:guid}")]
    [HasPermission("ClosingTasks.Reject")]
    public async Task<IActionResult> Reject(Guid id, RejectClosingTaskRequest request, CancellationToken cancellationToken)
    {
        return ApiResult(await service.RejectTaskAsync(id, request, cancellationToken));
    }

    [HttpPost("MarkTaskNotApplicable/{id:guid}")]
    [HasPermission("ClosingTasks.Manage")]
    public async Task<IActionResult> NotApplicable(Guid id, CancellationToken cancellationToken)
    {
        return ApiResult(await service.MarkNotApplicableAsync(id, cancellationToken));
    }
}