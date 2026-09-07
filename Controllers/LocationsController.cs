using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize]
[Route("api/locations")]
public sealed class LocationsController(LocationService locationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationItem>>> GetAll(CancellationToken cancellationToken)
    {
        var locations = await locationService.GetAllAsync(
            cancellationToken);

        return Ok(locations);
    }
}
