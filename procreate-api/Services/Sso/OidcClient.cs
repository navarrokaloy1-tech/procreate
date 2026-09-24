using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProCreateApi.Services.Sso;

/// <summary>
/// Caches the provider's discovery document and signing keys. Held as a
/// singleton because the underlying manager refreshes keys on its own
/// schedule; building one per request would re-fetch the document every time.
/// </summary>
public class OidcDiscovery
{
    private readonly SsoSettings _settings;
    private readonly ILogger<OidcDiscovery> _log;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _manager;

    public OidcDiscovery(SsoSettings settings, ILogger<OidcDiscovery> log)
    {
        _settings = settings;
        _log = log;

        if (!settings.IsConfigured) return;

        var metadata = settings.Authority.TrimEnd('/') + "/.well-known/openid-configuration";
        _manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadata,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = metadata.StartsWith("https://", StringComparison.OrdinalIgnoreCase) });
    }

    /// <summary>Null when SSO is switched off or the provider is unreachable.</summary>
    public async Task<OpenIdConnectConfiguration?> GetAsync(CancellationToken ct)
    {
        if (_manager is null) return null;
        try
        {
            return await _manager.GetConfigurationAsync(ct);
        }
        catch (Exception ex)
        {
            // A provider that is down must not take the whole sign-in screen
            // with it: the caller falls back to an error the user can read.
            // The reason only ever appears here, where an operator can act on
            // it — a misconfigured authority is otherwise invisible.
            _log.LogError(ex, "Could not read OIDC discovery from {Authority}", _settings.Authority);
            return null;
        }
    }
}

/// <summary>
/// The authorisation-code half of OIDC. The code is exchanged server side so
/// the client secret never reaches the browser, which is also what lets the
/// same flow serve a provider that refuses public clients.
/// </summary>
public class OidcClient
{
    private readonly SsoSettings _settings;
    private readonly OidcDiscovery _discovery;
    private readonly HttpClient _http;

    public OidcClient(SsoSettings settings, OidcDiscovery discovery, HttpClient http)
    {
        _settings = settings;
        _discovery = discovery;
        _http = http;
    }

    public Task<OpenIdConnectConfiguration?> GetConfigurationAsync(CancellationToken ct) =>
        _discovery.GetAsync(ct);

    public string BuildAuthorizeUrl(
        OpenIdConnectConfiguration config,
        string state,
        string nonce,
        string codeChallenge)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _settings.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _settings.CallbackUrl,
            ["scope"] = _settings.Scopes,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var pairs = query
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");

        return config.AuthorizationEndpoint + "?" + string.Join("&", pairs);
    }

    /// <summary>
    /// Trades the one-time code for an ID token. Returns null on any refusal;
    /// the reason belongs in the log, not in a message to the browser.
    /// </summary>
    public async Task<string?> ExchangeCodeAsync(
        OpenIdConnectConfiguration config,
        string code,
        string codeVerifier,
        CancellationToken ct)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _settings.CallbackUrl,
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["code_verifier"] = codeVerifier
        });

        using var response = await _http.PostAsync(config.TokenEndpoint, body, ct);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        return json.RootElement.TryGetProperty("id_token", out var idToken)
            ? idToken.GetString()
            : null;
    }

    /// <summary>
    /// Validates signature, issuer, audience, lifetime and nonce. The nonce
    /// check is what ties the token back to the request this browser started,
    /// so a token captured elsewhere cannot be replayed into our callback.
    /// </summary>
    public ClaimsPrincipal? ValidateIdToken(
        OpenIdConnectConfiguration config,
        string idToken,
        string expectedNonce)
    {
        var handler = new JwtSecurityTokenHandler
        {
            // Keep the wire claim names ("email", "groups") rather than the
            // SOAP-era URIs the handler substitutes by default.
            MapInboundClaims = false
        };

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.ClientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        try
        {
            var principal = handler.ValidateToken(idToken, parameters, out var validated);
            var nonce = (validated as JwtSecurityToken)?.Payload.TryGetValue("nonce", out var raw) == true
                ? raw as string
                : null;

            return string.Equals(nonce, expectedNonce, StringComparison.Ordinal) ? principal : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A URL-safe random value for state, nonce and PKCE verifier.</summary>
    public static string NewSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 challenge for a PKCE verifier.</summary>
    public static string CodeChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
