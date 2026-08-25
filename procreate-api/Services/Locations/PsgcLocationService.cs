using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace ProCreateApi.Services.Locations;

/// <summary>
/// Serves address reference data from a PSGC API, cached in memory, and falls
/// back to the built-in <see cref="LocationData"/> list whenever the remote
/// call is disabled, slow, or failing.
///
/// The fallback is the important part: patient registration must never be
/// blocked because a third-party lookup service is down.
/// </summary>
public class PsgcLocationService : ILocationService
{
    private const string PhCode = "PH";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly PsgcOptions _options;
    private readonly PsgcStatus _status;
    private readonly ILogger<PsgcLocationService> _log;

    public PsgcLocationService(
        HttpClient http,
        IMemoryCache cache,
        PsgcOptions options,
        PsgcStatus status,
        ILogger<PsgcLocationService> log)
    {
        _http = http;
        _cache = cache;
        _options = options;
        _status = status;
        _log = log;
    }

    // ------------------------------------------------------------
    // PSGC wire shapes
    // ------------------------------------------------------------

    private record PsgcRegion(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("regionName")] string? RegionName);

    private record PsgcNamed(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("name")] string Name);

    // ------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------

    public IReadOnlyList<Country> GetCountries() => LocationData.Countries;

    public async Task<IReadOnlyList<Region>> GetRegionsAsync(string? country, CancellationToken ct = default)
    {
        if (!IsPhilippines(country)) return Array.Empty<Region>();

        var remote = await GetOrFetchAsync("psgc:regions", async token =>
        {
            var regions = await _http.GetFromJsonAsync<List<PsgcRegion>>("regions/", token);

            return regions?
                .Select(r => new Region(r.Code, DisplayName(r)))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture)
                .ToList();
        }, ct);

        return remote ?? LocationData.RegionsFor(PhCode);
    }

    public async Task<IReadOnlyList<Province>> GetProvincesAsync(
        string? country, string? region, CancellationToken ct = default)
    {
        if (!IsPhilippines(country) || string.IsNullOrWhiteSpace(region))
            return Array.Empty<Province>();

        var regions = await GetRegionsAsync(country, ct);
        var match = MatchRegion(regions, region);
        if (match is null) return LocationData.ProvincesFor(PhCode, region);

        // Only PSGC codes are numeric; the offline list uses short codes like
        // "R11", which the remote API would 404 on.
        if (!IsPsgcCode(match.Code)) return LocationData.ProvincesFor(PhCode, region);

        var remote = await GetOrFetchAsync($"psgc:provinces:{match.Code}", async token =>
        {
            var provinces = await _http.GetFromJsonAsync<List<PsgcNamed>>(
                $"regions/{match.Code}/provinces/", token);

            // NCR has no provinces — it is subdivided into districts. Present a
            // single stand-in so the form's four levels stay consistent, and
            // serve its LGUs straight off the region.
            if (provinces is null || provinces.Count == 0)
            {
                return new List<Province> { new("Metro Manila") };
            }

            return provinces
                .Select(p => new Province(p.Name))
                .OrderBy(p => p.Name, StringComparer.CurrentCulture)
                .ToList();
        }, ct);

        return remote ?? LocationData.ProvincesFor(PhCode, region);
    }

    public async Task<IReadOnlyList<City>> GetCitiesAsync(
        string? country, string? province, CancellationToken ct = default)
    {
        if (!IsPhilippines(country) || string.IsNullOrWhiteSpace(province))
            return Array.Empty<City>();

        var code = await ResolveProvinceCodeAsync(province, ct);

        // "Metro Manila" is our own construct, so its LGUs hang off the NCR
        // region endpoint rather than a province one.
        if (code is null && province.Equals("Metro Manila", StringComparison.OrdinalIgnoreCase))
        {
            var ncr = await FindRegionCodeAsync("NCR", ct);
            if (ncr is not null)
            {
                var metro = await GetOrFetchAsync($"psgc:cities:region:{ncr}", token =>
                    FetchCitiesAsync($"regions/{ncr}/cities-municipalities/", token), ct);

                if (metro is not null) return metro;
            }
        }

        if (code is null) return LocationData.CitiesFor(PhCode, province);

        var remote = await GetOrFetchAsync($"psgc:cities:{code}", token =>
            FetchCitiesAsync($"provinces/{code}/cities-municipalities/", token), ct);

        return remote ?? LocationData.CitiesFor(PhCode, province);
    }

    public LocationSourceInfo Describe()
    {
        var (lastSuccess, lastError) = _status.Read();

        return new LocationSourceInfo(
            lastSuccess is null ? "offline" : "psgc",
            _options.Enabled,
            lastSuccess,
            lastError);
    }

    // ------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------

    private async Task<List<City>?> FetchCitiesAsync(string path, CancellationToken ct)
    {
        var cities = await _http.GetFromJsonAsync<List<PsgcNamed>>(path, ct);

        return cities?
            .Select(c => new City(Tidy(c.Name)))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// PSGC spells chartered cities "City of Tabuk" / "Tabuk City". Normalise to
    /// the bare place name so the dropdown reads the way people write addresses.
    /// </summary>
    private static string Tidy(string name)
    {
        const string prefix = "City of ";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return name[prefix.Length..].Trim();

        return name.Trim();
    }

    /// <summary>
    /// PSGC gives acronyms in `name` for NCR/CAR/BARMM and the descriptive
    /// title in `regionName`; for the rest it is the other way round. Prefer
    /// whichever is actually descriptive.
    /// </summary>
    private static string DisplayName(PsgcRegion region)
    {
        if (region.Name.Length <= 5 && !string.IsNullOrWhiteSpace(region.RegionName))
            return region.RegionName;

        return region.Name;
    }

    private async Task<string?> FindRegionCodeAsync(string nameOrCode, CancellationToken ct)
    {
        var regions = await GetRegionsAsync(PhCode, ct);
        var match = MatchRegion(regions, nameOrCode)
            ?? regions.FirstOrDefault(r => r.Name.Contains("National Capital", StringComparison.OrdinalIgnoreCase));

        return match is not null && IsPsgcCode(match.Code) ? match.Code : null;
    }

    private async Task<string?> ResolveProvinceCodeAsync(string province, CancellationToken ct)
    {
        // Provinces are only addressable by code, and codes live per-region, so
        // walk the regions until the name matches. Every call is cached.
        var regions = await GetRegionsAsync(PhCode, ct);

        foreach (var region in regions.Where(r => IsPsgcCode(r.Code)))
        {
            var provinces = await GetOrFetchAsync($"psgc:province-codes:{region.Code}", async token =>
                await _http.GetFromJsonAsync<List<PsgcNamed>>(
                    $"regions/{region.Code}/provinces/", token), ct);

            var match = provinces?.FirstOrDefault(p =>
                string.Equals(p.Name, province, StringComparison.OrdinalIgnoreCase));

            if (match is not null) return match.Code;
        }

        return null;
    }

    private static Region? MatchRegion(IReadOnlyList<Region> regions, string nameOrCode) =>
        regions.FirstOrDefault(r =>
            string.Equals(r.Code, nameOrCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.Name, nameOrCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>PSGC codes are 9-10 digit numerics; our offline codes are not.</summary>
    private static bool IsPsgcCode(string code) =>
        code.Length >= 9 && code.All(char.IsDigit);

    private static bool IsPhilippines(string? country) =>
        !string.IsNullOrWhiteSpace(country) &&
        (country.Equals(PhCode, StringComparison.OrdinalIgnoreCase) ||
         country.Equals("Philippines", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Cache-aside around a remote fetch. Returns null on any failure so the
    /// caller can fall back; failures are cached briefly to avoid hammering a
    /// service that is already struggling.
    /// </summary>
    private async Task<List<T>?> GetOrFetchAsync<T>(
        string key, Func<CancellationToken, Task<List<T>?>> fetch, CancellationToken ct)
    {
        if (!_options.Enabled) return null;

        if (_cache.TryGetValue(key, out List<T>? cached)) return cached;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var result = await fetch(timeout.Token);

            if (result is null || result.Count == 0) return null;

            _cache.Set(key, result, TimeSpan.FromHours(_options.CacheHours));
            _status.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            _status.RecordFailure(ex.GetBaseException().Message);
            _log.LogWarning(ex,
                "PSGC lookup failed for {Key}; serving the offline address list instead.", key);

            // Brief negative cache. Kept short because the first outbound TLS
            // handshake from a cold process can be reset by local security
            // software, and a long hold would strand users on the short list.
            _cache.Set<List<T>?>(key, null, TimeSpan.FromSeconds(20));
            return null;
        }
    }
}
