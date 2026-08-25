using ProCreateApi.Services.Locations;
using Microsoft.AspNetCore.Mvc;

namespace ProCreateApi.Controllers;

/// <summary>
/// Read-only address reference data backing the cascading country / region /
/// province / city dropdowns on the patient registration form.
///
/// Served from a PSGC API where reachable, otherwise from the built-in offline
/// list — see <see cref="PsgcLocationService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locations;

    public LocationsController(ILocationService locations) { _locations = locations; }

    [HttpGet("countries")]
    public IActionResult GetCountries() => Ok(_locations.GetCountries());

    /// <summary>Regions within a country. Empty when we hold no data for it.</summary>
    [HttpGet("regions")]
    public async Task<IActionResult> GetRegions([FromQuery] string? country, CancellationToken ct)
        => Ok(await _locations.GetRegionsAsync(country, ct));

    /// <summary>Provinces within a region. Empty when we hold no data for it.</summary>
    [HttpGet("provinces")]
    public async Task<IActionResult> GetProvinces(
        [FromQuery] string? country, [FromQuery] string? region, CancellationToken ct)
        => Ok(await _locations.GetProvincesAsync(country, region, ct));

    /// <summary>Cities and municipalities within a province.</summary>
    [HttpGet("cities")]
    public async Task<IActionResult> GetCities(
        [FromQuery] string? country, [FromQuery] string? province, CancellationToken ct)
        => Ok(await _locations.GetCitiesAsync(country, province, ct));

    /// <summary>
    /// Whether live PSGC data or the offline fallback is currently in use.
    /// Useful when a dropdown looks shorter than expected.
    /// </summary>
    [HttpGet("source")]
    public IActionResult GetSource() => Ok(_locations.Describe());
}
