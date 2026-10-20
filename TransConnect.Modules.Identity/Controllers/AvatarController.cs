using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Controllers;

[ApiController, Authorize, Route("auth/me/avatar")]
public sealed class AvatarController(AvatarService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(5300000)]
    public async Task<ActionResult<ApiResponse<object>>> Upload(IFormFile file, CancellationToken ct) =>
        Ok(ApiResponse<object>.Success(new { avatarUrl = await service.UploadAsync(file, ct) }));
}
