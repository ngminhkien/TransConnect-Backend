using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Auth;
using TransConnect.Common.Response;
using TransConnect.Modules.Locations.Dto.Requests;
using TransConnect.Modules.Locations.Services;
namespace TransConnect.Modules.Locations.Controllers;

[ApiController, Authorize, RequirePermission("locations.manage"), Route("locations/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LocationsAdminController(ILocationsService service) : ControllerBase
{
    private IActionResult Result<T>(T data) => Ok(ApiResponse<T>.Success(data));
    [HttpGet("shared")]
    public async Task<IActionResult> Shared(CancellationToken ct, [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        => Result(await service.AdminSharedAsync(search, pageNumber, pageSize, ct));
    [HttpPost("datasets/import"), RequestSizeLimit(10000000)]
    public async Task<IActionResult> Import(ImportDatasetRequest request, CancellationToken ct) => Result(await service.ImportAsync(request, ct));
    [HttpPost("shared")]
    public async Task<IActionResult> Create(SaveSharedLocationRequest request, CancellationToken ct) => Result(await service.SaveSharedAsync(null, request, ct));
    [HttpPut("shared/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveSharedLocationRequest request, CancellationToken ct) => Result(await service.SaveSharedAsync(id, request, ct));
    [HttpDelete("shared/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, ArchiveLocationRequest request, CancellationToken ct)
    {
        await service.ArchiveSharedAsync(id, request, ct); return Result(new { id });
    }
    [HttpGet("shared/{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) => Result(await service.HistoryAsync(id, ct));
    [HttpGet("requests")]
    public async Task<IActionResult> Requests(CancellationToken ct, [FromQuery] string? status = null)
        => Result(await service.RequestsAsync(null, status, true, ct));
    [HttpPost("requests/{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, ReviewLocationRequest request, CancellationToken ct) => Result(await service.ReviewAsync(id, request, ct));
}
