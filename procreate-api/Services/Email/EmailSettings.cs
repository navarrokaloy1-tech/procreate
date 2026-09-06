namespace ProCreateApi.Services.Email;

/// <summary>
/// SMTP details, bound from the "Email" section of appsettings.
///
/// Ships blank on purpose. Result emails carry patient information, so sending
/// is refused with a clear message until a real account is configured rather
/// than quietly falling back to some default.
/// </summary>
public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Pro-Create Fertility and OB-GYN Clinic";
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>A host and a from-address are the minimum to attempt a send.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
