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
        if (file.Length == 0) return BadRequest(Shared.Responses.BaseResponseDto<object>.Fail("Validation failed.", ["File is required."]));
        await using var stream = file.OpenReadStream();
        return ApiResult(await service.UploadAsync(request, file.FileName, file.ContentType, file.Length, stream, cancellationToken));
    }

    [HttpGet("DownloadDocument/{id:guid}")]
    [HasPermission("Documents.Download")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var response = await service.DownloadAsync(id, cancellationToken);
        if (!response.Success || response.Data is null) return BadRequest(response);
        var file = response.Data;
        return PhysicalFile(file.FilePath, file.ContentType, file.OriginalFileName);
    }

    [HttpDelete("DeleteDocument/{id:guid}")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) => ApiResult(await service.DeleteAsync(id, cancellationToken));
}
