using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController(IDocumentService service) : AccountingControllerBase
{
    [HttpGet("GetDocumentsByFilter")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> GetPaged([FromQuery] AccountingPagedRequest request, CancellationToken cancellationToken) => ApiResult(await service.GetPagedAsync(request, cancellationToken));

    [HttpPost("UploadDocument")]
    [HasPermission("Documents.Upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return ApiResult(Shared.Responses.BaseResponseDto<object>.Fail("الملف مطلوب."));
        await using var stream = file.OpenReadStream();
        return ApiResult(await service.UploadAsync(request, file.FileName, file.ContentType, file.Length, stream, cancellationToken));
    }

    [HttpGet("DownloadDocument/{id:guid}")]
    [HasPermission("Documents.Download")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.DownloadAsync(id, cancellationToken);
        if (!response.Success || response.Data is null) return ApiResult(response);
        var file = response.Data;
        return PhysicalFile(file.FilePath, file.ContentType, file.OriginalFileName);
    }

    [HttpDelete("DeleteDocument/{id:guid}")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) => ApiResult(await service.DeleteAsync(id, cancellationToken));

    [HttpPost("SubmitForReview/{id:guid}")]
    [HasPermission("Documents.Submit")]
    public async Task<IActionResult> SubmitForReview(Guid id, SubmitWorkflowRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.SubmitForReviewAsync(id, request, cancellationToken));

    [HttpPost("StartReview/{id:guid}")]
    [HasPermission("Documents.Review")]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken cancellationToken) =>
        ApiResult(await service.StartReviewAsync(id, cancellationToken));

    [HttpPost("Approve/{id:guid}")]
    [HasPermission("Documents.Approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        ApiResult(await service.ApproveAsync(id, cancellationToken));

    [HttpPost("Reject/{id:guid}")]
    [HasPermission("Documents.Reject")]
    public async Task<IActionResult> Reject(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.RejectAsync(id, request, cancellationToken));

    [HttpPost("ReturnForCorrection/{id:guid}")]
    [HasPermission("Documents.Reject")]
    public async Task<IActionResult> ReturnForCorrection(Guid id, WorkflowDecisionRequest request, CancellationToken cancellationToken) =>
        ApiResult(await service.ReturnForCorrectionAsync(id, request, cancellationToken));
}
