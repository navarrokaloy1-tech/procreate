using System.Net;
using System.Net.Mail;

namespace ProCreateApi.Services.Email;

/// <summary>One file to hang off an outgoing message.</summary>
public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public record EmailResult(bool Sent, string FailureReason)
{
    public static EmailResult Ok() => new(true, string.Empty);
    public static EmailResult Failed(string reason) => new(false, reason);
}

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task<EmailResult> SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Plain SMTP over <see cref="SmtpClient"/>. Works against a clinic mail server
/// or a Gmail app password without pulling in another dependency — this project
/// otherwise has none beyond EF Core and BCrypt.
/// </summary>
public class EmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(EmailSettings settings, ILogger<EmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<EmailResult> SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return EmailResult.Failed(
                "Email is not configured. Set Email:Host and Email:FromAddress in appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(toAddress))
            return EmailResult.Failed("No email address to send to.");

        // Held open until the send completes; attachments stream from these.
        var streams = new List<MemoryStream>();

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(toAddress));

            foreach (var attachment in attachments ?? Enumerable.Empty<EmailAttachment>())
            {
                var stream = new MemoryStream(attachment.Content);
                streams.Add(stream);

                message.Attachments.Add(new Attachment(
                    stream,
                    attachment.FileName,
                    string.IsNullOrWhiteSpace(attachment.ContentType)
                        ? "application/octet-stream"
                        : attachment.ContentType));
            }

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Timeout = _settings.TimeoutSeconds * 1000
            };

            // An anonymous relay is legitimate on an internal mail server, so
            // credentials are only attached when they were actually given.
            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            return EmailResult.Ok();
        }
        catch (Exception ex)
        {
            // The address and the failure matter for the audit row; the stack
            // does not, and this is on a request path a user is waiting on.
            _logger.LogError(ex, "Sending results to {Address} failed", toAddress);
            return EmailResult.Failed(ex.Message);
        }
        finally
        {
            foreach (var stream in streams) await stream.DisposeAsync();
        }
    }
}
