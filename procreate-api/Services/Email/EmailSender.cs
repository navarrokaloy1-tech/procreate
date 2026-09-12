using System.Net;
using System.Net.Mail;

namespace ProCreateApi.Services.Email;

/// <summary>
/// One file to hang off an outgoing message.
///
/// Give <paramref name="ContentId"/> to embed it in the body instead, referred
/// to as cid:that-id. Mail clients strip data: URIs, so an image that has to
/// appear inline — a signature — has to travel this way.
/// </summary>
public record EmailAttachment(
    string FileName, string ContentType, byte[] Content, string? ContentId = null);

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
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(toAddress));

            var all = (attachments ?? Enumerable.Empty<EmailAttachment>()).ToList();
            var inline = all.Where(a => !string.IsNullOrWhiteSpace(a.ContentId)).ToList();
            var files = all.Where(a => string.IsNullOrWhiteSpace(a.ContentId)).ToList();

            // Anything to embed has to hang off an alternate view, which is what
            // gives cid: references something to resolve against.
            if (inline.Count > 0)
            {
                var view = AlternateView.CreateAlternateViewFromString(
                    htmlBody, null, "text/html");

                foreach (var embedded in inline)
                {
                    var stream = new MemoryStream(embedded.Content);
                    streams.Add(stream);

                    view.LinkedResources.Add(new LinkedResource(stream, embedded.ContentType)
                    {
                        ContentId = embedded.ContentId,
                        // Without this some clients list it as an attachment as
                        // well as drawing it in the body.
                        TransferEncoding = System.Net.Mime.TransferEncoding.Base64,
                        ContentLink = new Uri($"cid:{embedded.ContentId}")
                    });
                }

                message.AlternateViews.Add(view);
            }
            else
            {
                message.Body = htmlBody;
            }

            foreach (var attachment in files)
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
