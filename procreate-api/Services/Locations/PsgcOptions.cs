namespace ProCreateApi.Services.Locations;

/// <summary>
/// Configuration for the Philippine Standard Geographic Code lookup service.
/// Bound from the "Psgc" section of appsettings.
/// </summary>
public class PsgcOptions
{
    /// <summary>
    /// When false the API serves only the built-in offline dataset and never
    /// makes an outbound call. Turn this off for air-gapped deployments.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Base URL of a PSGC API. Swap this for a self-hosted mirror to remove the
    /// third-party runtime dependency.
    /// </summary>
    public string BaseUrl { get; set; } = "https://psgc.gitlab.io/api";

    /// <summary>
    /// How long a fetched list is reused before refetching. PSGC changes on the
    /// order of years, so this is deliberately long.
    /// </summary>
    public int CacheHours { get; set; } = 720;

    /// <summary>Per-request timeout. Kept short: the offline fallback is cheap.</summary>
    public int TimeoutSeconds { get; set; } = 8;
}
