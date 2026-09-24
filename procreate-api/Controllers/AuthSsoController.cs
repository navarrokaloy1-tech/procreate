using ProCreateApi.Data;
using ProCreateApi.Services.Auth;
using ProCreateApi.Services.Sso;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ProCreateApi.Controllers;

/// <summary>
/// Single sign-on. The browser is sent to the identity provider, comes back
/// here with a code, and leaves with a one-time ticket it swaps for an
/// ordinary session token — after which nothing downstream can tell an SSO
/// session from a password one.
///
/// Accounts are never created here. The provider proves who is at the
/// keyboard; the record still has to exist in this system, which keeps
/// admission to the console an administrator's decision.
/// </summary>
[ApiController]
[Route("api/auth/sso")]
public class AuthSsoController : ControllerBase
{
    private const string StaffAudience = "staff";
    private const string PatientAudience = "patient";

    private readonly AppDbContext _db;
    private readonly SsoSettings _settings;
    private readonly OidcClient _oidc;
    private readonly SsoHandoff _handoff;
    private readonly TokenIssuer _tokens;
    private readonly ILogger<AuthSsoController> _log;

    public AuthSsoController(
        AppDbContext db,
        SsoSettings settings,
        OidcClient oidc,
        SsoHandoff handoff,
        TokenIssuer tokens,
        ILogger<AuthSsoController> log)
    {
        _db = db;
        _settings = settings;
        _oidc = oidc;
        _handoff = handoff;
        _tokens = tokens;
        _log = log;
    }

    /// <summary>
    /// Lets the sign-in screens decide whether to offer the button at all,
    /// so an unconfigured install shows password sign-in and nothing else.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(new
    {
        enabled = _settings.IsConfigured,
        displayName = _settings.DisplayName
    });

    /// <summary>
    /// Leaves for the provider. A top-level redirect rather than a fetch,
    /// because the provider may need to show its own screens — and with
    /// Authentik in front of Google, usually will.
    /// </summary>
    [HttpGet("start")]
    public async Task<IActionResult> Start([FromQuery] string? audience, CancellationToken ct)
    {
        var target = Normalise(audience);

        if (!_settings.IsConfigured)
            return Redirect(FailureUrl(target, "Single sign-on is not set up."));

        var config = await _oidc.GetConfigurationAsync(ct);
        if (config?.AuthorizationEndpoint is null)
        {
            _log.LogWarning("SSO discovery failed for authority {Authority}", _settings.Authority);
            return Redirect(FailureUrl(target, "The sign-in service could not be reached."));
        }

        var verifier = OidcClient.NewSecret();
        var nonce = OidcClient.NewSecret();
        var state = _handoff.StashRequest(new SsoRequest(nonce, verifier, target));

        return Redirect(_oidc.BuildAuthorizeUrl(config, state, nonce, OidcClient.CodeChallenge(verifier)));
    }

    /// <summary>
    /// Where the provider returns the browser. Everything that can go wrong
    /// ends the same way — back at the sign-in screen with something readable
    /// — because this response is a page the user sees, not an API reply.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        // Taken first: a state that is replayed or has expired must not get a
        // second attempt, whatever else the provider sent back.
        var request = _handoff.TakeRequest(state);
        var target = Normalise(request?.Audience);

        if (request is null)
            return Redirect(FailureUrl(target, "That sign-in took too long. Please try again."));

        if (!string.IsNullOrEmpty(error))
        {
            _log.LogInformation("SSO provider refused the sign-in: {Error}", error);
            return Redirect(FailureUrl(target, "The sign-in was cancelled or refused."));
        }

        if (string.IsNullOrEmpty(code))
            return Redirect(FailureUrl(target, "The sign-in did not complete."));

        var config = await _oidc.GetConfigurationAsync(ct);
        if (config is null)
            return Redirect(FailureUrl(target, "The sign-in service could not be reached."));

        var idToken = await _oidc.ExchangeCodeAsync(config, code, request.CodeVerifier, ct);
        if (idToken is null)
        {
            _log.LogWarning("SSO code exchange was refused by {Authority}", _settings.Authority);
            return Redirect(FailureUrl(target, "The sign-in could not be verified."));
        }

        var principal = _oidc.ValidateIdToken(config, idToken, request.Nonce);
        if (principal is null)
        {
            _log.LogWarning("SSO token failed validation for {Authority}", _settings.Authority);
            return Redirect(FailureUrl(target, "The sign-in could not be verified."));
        }

        var email = EmailOf(principal);
        if (email is null)
            return Redirect(FailureUrl(target, "That account has no confirmed email address."));

        var session = target == PatientAudience
            ? await PatientSessionAsync(email)
            : await StaffSessionAsync(email, principal);

        if (session is null)
        {
            // Naming the address would confirm to a stranger which ones are
            // registered here, so the reply says only what to do next.
            return Redirect(FailureUrl(target, target == PatientAudience
                ? "No patient account matches that sign-in. Please register first."
                : "That account is not set up for staff access here."));
        }

        var ticket = Uri.EscapeDataString(_handoff.StashSession(session));
        return Redirect($"{AppBase()}/auth/callback?ticket={ticket}&audience={target}");
    }

    /// <summary>
    /// Swaps the one-time ticket for the session. Kept off the redirect so
    /// the token never appears in a URL.
    /// </summary>
    [HttpPost("exchange")]
    public IActionResult Exchange([FromBody] SsoExchangeRequest req)
    {
        var session = _handoff.TakeSession(req.Ticket);
        return session is null
            ? Unauthorized(new { message = "That sign-in has expired. Please try again." })
            : Ok(session);
    }

    // ----------------------------------------------------------
    // Matching a provider identity to a record here
    // ----------------------------------------------------------

    private async Task<object?> StaffSessionAsync(string email, ClaimsPrincipal principal)
    {
        if (_settings.StaffGroup.Length > 0 && !IsInStaffGroup(principal))
        {
            _log.LogInformation("SSO sign-in refused: not a member of {Group}", _settings.StaffGroup);
            return null;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.IsActive && u.Email != "" && u.Email.ToLower() == email);

        return user is null ? null : await _tokens.CreateStaffSessionAsync(user);
    }

    private async Task<object?> PatientSessionAsync(string email)
    {
        // Same bar as password sign-in: the portal has to have been enabled
        // for that record already. A matching address alone is not enough to
        // hand over someone's medical history.
        var patient = await _db.Patients
            .FirstOrDefaultAsync(p => p.PortalEnabled && p.Email != "" && p.Email.ToLower() == email);

        return patient is null ? null : await _tokens.CreatePatientSessionAsync(patient);
    }

    private bool IsInStaffGroup(ClaimsPrincipal principal) =>
        principal.FindAll("groups")
            .Any(c => string.Equals(c.Value, _settings.StaffGroup, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The address to match on, or null if the provider will not vouch for
    /// it. An unverified address is an unproven claim to someone's record.
    /// </summary>
    private static string? EmailOf(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst("email")?.Value?.Trim();
        if (string.IsNullOrEmpty(email)) return null;

        var verified = principal.FindFirst("email_verified")?.Value;
        if (verified is not null && !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
            return null;

        return email.ToLowerInvariant();
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private static string Normalise(string? audience) =>
        string.Equals(audience, PatientAudience, StringComparison.OrdinalIgnoreCase)
            ? PatientAudience
            : StaffAudience;

    private string AppBase() => _settings.AppBaseUrl.TrimEnd('/');

    private string FailureUrl(string audience, string message) =>
        $"{AppBase()}/auth/callback?audience={audience}&error={Uri.EscapeDataString(message)}";
}

public record SsoExchangeRequest(string? Ticket);
