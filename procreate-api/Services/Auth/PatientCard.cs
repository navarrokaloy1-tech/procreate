using System.Security.Cryptography;

namespace ProCreateApi.Services.Auth;

/// <summary>
/// The patient card QR.
///
/// The card is no longer a way to sign in — that is single sign-on now. It is
/// a way for a doctor, med-tech, nurse or cashier to put their hands on the
/// right record in one scan, which is why every patient still gets one at
/// registration.
/// </summary>
public static class PatientCard
{
    /// <summary>The scheme a printed card carries in front of its token.</summary>
    public const string Scheme = "procreate-card:";

    /// <summary>
    /// A URL-safe random secret. Random rather than the patient code, which
    /// runs in sequence and could therefore be guessed by counting.
    /// </summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>
    /// Strips the card scheme if present, so a scanner can hand the raw
    /// payload straight over.
    /// </summary>
    public static string StripScheme(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        return trimmed.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            ? trimmed[Scheme.Length..].Trim()
            : trimmed;
    }

    /// <summary>Whether a scanned payload is a card rather than a typed code.</summary>
    public static bool IsCardPayload(string? raw) =>
        (raw ?? string.Empty).TrimStart().StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);
}
