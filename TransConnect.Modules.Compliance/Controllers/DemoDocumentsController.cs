using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Dto.Responses;
using TransConnect.Modules.Compliance.Services;

namespace TransConnect.Modules.Compliance.Controllers;

[ApiController]
[Authorize]
[Route("compliance/demo/documents")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DemoDocumentsController(IDemoDocumentService service) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DemoDocumentService.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = DemoDocumentService.MaxBytes + 65536)]
    public async Task<ActionResult<ApiResponse<DemoDocumentResponse>>> Upload([FromForm] UploadDemoDocumentRequest request, CancellationToken ct)
        => Ok(ApiResponse<DemoDocumentResponse>.Success(await service.UploadAsync(request, ct)));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DemoDocumentResponse>>>> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<DemoDocumentResponse>>.Success(await service.ListAsync(ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DemoDocumentResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<DemoDocumentResponse>.Success(await service.GetAsync(id, ct)));

    [HttpPost("{id:guid}/simulate")]
    public async Task<ActionResult<ApiResponse<DemoDocumentResponse>>> Simulate(Guid id, SimulateDocumentRequest request, CancellationToken ct)
        => Ok(ApiResponse<DemoDocumentResponse>.Success(await service.SimulateAsync(id, request, ct)));

    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<ApiResponse<DemoDocumentResponse>>> Review(Guid id, ReviewDemoDocumentRequest request, CancellationToken ct)
        => Ok(ApiResponse<DemoDocumentResponse>.Success(await service.ReviewAsync(id, request, ct)));

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await service.DownloadAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["X-Document-Mode"] = "DEMO-UNSCANNED";
        return File(result.Content, "application/octet-stream", result.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Success(new { id }, "Đã xóa file demo."));
    }
}
