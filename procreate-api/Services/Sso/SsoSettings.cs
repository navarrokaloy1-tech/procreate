namespace ProCreateApi.Services.Sso;

/// <summary>
/// Single sign-on against an OpenID Connect provider.
///
/// Written against plain OIDC rather than any one vendor, so Authority can
/// point at Authentik, Entra ID, Google or Keycloak without a code change.
/// The clinic runs Authentik, which brokers Google as a login source and
/// keeps enrolment and policy in one place — see docs/sso-authentik.md.
///
/// Blank settings are valid: SSO reports itself disabled, the sign-in screens
/// hide the button, and password sign-in carries on as before.
/// </summary>
public class SsoSettings
{
    public bool Enabled { get; set; }

    /// <summary>
    /// OIDC issuer, e.g. "https://auth.example.com/application/o/procreate/".
    /// Discovery is read from "{Authority}/.well-known/openid-configuration".
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public string Scopes { get; set; } = "openid email profile";

    /// <summary>
    /// Absolute redirect URI. Must be registered with the provider character
    /// for character, so it is configured rather than derived from the request.
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>Where the browser is sent once the session exists.</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Names the method on the sign-in button, which reads "Log in with
    /// {DisplayName}". Left as "SSO" so the button does not have to be
    /// re-explained to staff when the provider behind it changes.
    /// </summary>
    public string DisplayName { get; set; } = "SSO";

    /// <summary>
    /// Optional. When set, a staff sign-in must carry this value in its
    /// "groups" claim — a second gate behind whatever policy the provider
    /// already applies, so a patient-only account cannot reach the console
    /// even if it shares an address with a staff record.
    /// </summary>
    public string StaffGroup { get; set; } = string.Empty;

    /// <summary>
    /// Whether the provider is usable. Anything missing means "off" rather
    /// than a half-configured flow that fails at the redirect.
    /// </summary>
    public bool IsConfigured =>
        Enabled &&
        Authority.Length > 0 &&
        ClientId.Length > 0 &&
        ClientSecret.Length > 0 &&
        CallbackUrl.Length > 0;
}
