using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Locations.Services;

namespace TransConnect.Modules.Locations.Controllers;

[ApiController, Authorize, Route("locations/place-search")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlaceSearchController(PlaceSearchService search) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? query, CancellationToken ct)
        => Ok(ApiResponse<object>.Success(await search.SearchAsync(query, ct)));
}
