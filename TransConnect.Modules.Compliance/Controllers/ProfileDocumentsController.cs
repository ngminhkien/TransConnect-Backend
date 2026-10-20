using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Dto.Responses;
using TransConnect.Modules.Compliance.Services;

namespace TransConnect.Modules.Compliance.Controllers;

[ApiController, Authorize]
[Route("compliance/demo/profile-documents")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileDocumentsController(IProfileDocumentService service) : ControllerBase
{
    [HttpPost("{subjectType}/{subjectId:guid}/{documentType}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DemoDocumentService.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = DemoDocumentService.MaxBytes + 65536)]
    public async Task<ActionResult<ApiResponse<ProfileDocumentVersionResponse>>> Upload(string subjectType, Guid subjectId,
        string documentType, [FromForm] UploadDemoDocumentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ProfileDocumentVersionResponse>.Success(await service.UploadAsync(subjectType, subjectId, documentType, request, ct)));

    [HttpGet("{subjectType}/{subjectId:guid}/{documentType}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProfileDocumentVersionResponse>>>> List(string subjectType,
        Guid subjectId, string documentType, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ProfileDocumentVersionResponse>>.Success(await service.ListAsync(subjectType, subjectId, documentType, ct)));

    [HttpPost("versions/{id:guid}/simulate")]
    public async Task<ActionResult<ApiResponse<ProfileDocumentVersionResponse>>> Simulate(Guid id, SimulateDocumentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ProfileDocumentVersionResponse>.Success(await service.SimulateAsync(id, request, ct)));

    [HttpPost("versions/{id:guid}/review")]
    public async Task<ActionResult<ApiResponse<ProfileDocumentVersionResponse>>> Review(Guid id, ReviewDemoDocumentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ProfileDocumentVersionResponse>.Success(await service.ReviewAsync(id, request, ct)));

    [HttpGet("versions/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await service.DownloadAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["X-Document-Mode"] = "DEMO-UNSCANNED";
        return File(file.Content, "application/octet-stream", file.FileName);
    }
    [HttpDelete("versions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Success(new { id }));
    }
}
