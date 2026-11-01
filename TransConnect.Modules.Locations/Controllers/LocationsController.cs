using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Locations.Dto.Requests;
using TransConnect.Modules.Locations.Services;
namespace TransConnect.Modules.Locations.Controllers;

[ApiController, Authorize, Route("locations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LocationsController(ILocationsService service) : ControllerBase
{
    private IActionResult Result<T>(T data) => Ok(ApiResponse<T>.Success(data));
    [HttpGet("dataset")]
    public async Task<IActionResult> Dataset(CancellationToken ct) => Result(await service.ActiveDatasetAsync(ct));
    [HttpGet("provinces")]
    public async Task<IActionResult> Provinces(CancellationToken ct) => Result(await service.ProvincesAsync(ct));
    [HttpGet("wards")]
    public async Task<IActionResult> Wards([FromQuery] string provinceCode, CancellationToken ct) => Result(await service.WardsAsync(provinceCode, ct));
    [HttpGet("my-addresses")]
    public async Task<IActionResult> Addresses(CancellationToken ct) => Result(await service.AddressesAsync(ct));
    [HttpGet("my-addresses/{id:guid}")]
    public async Task<IActionResult> Address(Guid id, CancellationToken ct) => Result(await service.AddressAsync(id, ct));
    [HttpPost("my-addresses")]
    public async Task<IActionResult> CreateAddress(SaveAddressRequest request, CancellationToken ct) => Result(await service.SaveAddressAsync(null, request, ct));
    [HttpPut("my-addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid id, SaveAddressRequest request, CancellationToken ct) => Result(await service.SaveAddressAsync(id, request, ct));
    [HttpDelete("my-addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, [FromQuery] int version, CancellationToken ct)
    {
        await service.ArchiveAddressAsync(id, version, ct); return Result(new { id });
    }
    [HttpGet("shared")]
    public async Task<IActionResult> Shared(CancellationToken ct, [FromQuery] string? search = null, [FromQuery] string? provinceCode = null,
        [FromQuery] string? type = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        => Result(await service.SharedAsync(search, provinceCode, type, pageNumber, pageSize, ct));
    [HttpGet("shared/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Result(await service.SharedDetailAsync(id, ct));
    [HttpGet("requests")]
    public async Task<IActionResult> Requests([FromQuery] Guid organizationId, CancellationToken ct)
        => Result(await service.RequestsAsync(organizationId, null, false, ct));
    [HttpPost("requests")]
    public async Task<IActionResult> Submit(SubmitLocationRequest request, CancellationToken ct) => Result(await service.SubmitAsync(request, ct));
}
