using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ImportsController(IImportService service) : AccountingControllerBase
{
    [HttpPost("Upload")]
    [HasPermission("Imports.Upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadImportRequest request, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return ApiResult(BaseResponseDto<object>.Fail("الملف مطلوب."));
        }

        await using var stream = file.OpenReadStream();
        return ApiResult(await service.UploadAndValidateAsync(request, file.FileName, file.ContentType, file.Length, stream, cancellationToken));
    }

    [HttpGet("GetImportsByFilter")]
    [HasPermission("Imports.View")]
    public async Task<IActionResult> GetBatches([FromQuery] ImportBatchQuery query, CancellationToken cancellationToken) =>
        ApiResult(await service.GetBatchesAsync(query, cancellationToken));

    [HttpGet("GetImportById/{id:guid}")]
    [HasPermission("Imports.View")]
    public async Task<IActionResult> GetBatch(Guid id, [FromQuery] ImportBatchRowsQuery query, CancellationToken cancellationToken) =>
        ApiResult(await service.GetBatchDetailsAsync(id, query, cancellationToken));

    [HttpPost("Commit/{id:guid}")]
    [HasPermission("Imports.Confirm")]
    public async Task<IActionResult> Confirm(Guid id, ConfirmImportRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.ConfirmImportAsync(id, request, cancellationToken));

    [HttpPost("Cancel/{id:guid}")]
    [HasPermission("Imports.Cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelImportRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.CancelImportAsync(id, request, cancellationToken));

    [HttpGet("DownloadTemplate/{importType}")]
    [HasPermission("Imports.DownloadTemplate")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Template(ImportType importType, CancellationToken cancellationToken)
    {
        var response = await service.GenerateTemplateAsync(importType, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return ApiResult(response);
        }

        return File(response.Data.Content, response.Data.ContentType, response.Data.FileName);
    }

    [HttpPost("SubmitForReview/{id:guid}")]
    [HasPermission("Imports.Submit")]
    public async Task<IActionResult> SubmitForReview(Guid id, SubmitWorkflowRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.SubmitForReviewAsync(id, request, cancellationToken));

    [HttpPost("StartReview/{id:guid}")]
    [HasPermission("Imports.Review")]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken cancellationToken) =>
        ApiResult(await service.StartReviewAsync(id, cancellationToken));

    [HttpPost("Approve/{id:guid}")]
    [HasPermission("Imports.Approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        ApiResult(await service.ApproveAsync(id, cancellationToken));

    [HttpPost("Reject/{id:guid}")]
    [HasPermission("Imports.Reject")]
    public async Task<IActionResult> Reject(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.RejectAsync(id, request, cancellationToken));

    [HttpPost("ReturnForCorrection/{id:guid}")]
    [HasPermission("Imports.Reject")]
    public async Task<IActionResult> ReturnForCorrection(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.ReturnForCorrectionAsync(id, request, cancellationToken));
}
