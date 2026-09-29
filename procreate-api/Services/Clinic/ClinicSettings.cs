namespace ProCreateApi.Services.Clinic;

/// <summary>
/// The clinic's own identity, as it appears on the letterhead of a result
/// sheet — bound from the "Clinic" section of appsettings.
///
/// These are printed on documents a patient keeps, so they live in config
/// rather than in markup: a licence number or mobile number changes without
/// a redeploy, and the same values feed the printed sheet, the preview and
/// the emailed copy from one place.
/// </summary>
public class ClinicSettings
{
    public string Name { get; set; } = "Pro-Create Fertility and OB-GYN Clinic";

    /// <summary>The two lines under the wordmark on the letterhead.</summary>
    public string Tagline { get; set; } = "Fertility and OB-GYN Clinic";
    public string SubTagline { get; set; } = "Multi-Specialty and Diagnostic Center";

    public string DohLicenseNumber { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Shown beside the QR on the printed sheet, when set.</summary>
    public string Website { get; set; } = string.Empty;
}
