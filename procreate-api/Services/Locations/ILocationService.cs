namespace ProCreateApi.Services.Locations;

public interface ILocationService
{
    IReadOnlyList<Country> GetCountries();

    Task<IReadOnlyList<Region>> GetRegionsAsync(string? country, CancellationToken ct = default);

    Task<IReadOnlyList<Province>> GetProvincesAsync(
        string? country, string? region, CancellationToken ct = default);

    Task<IReadOnlyList<City>> GetCitiesAsync(
        string? country, string? province, CancellationToken ct = default);

    /// <summary>Where the last answer came from, surfaced for diagnostics.</summary>
    LocationSourceInfo Describe();
}

/// <param name="Source">"psgc" when live data is in use, "offline" for the built-in list.</param>
public record LocationSourceInfo(string Source, bool RemoteEnabled, DateTime? LastRemoteSuccess, string? LastError);
