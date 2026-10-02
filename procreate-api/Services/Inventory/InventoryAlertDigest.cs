using System.Text;
using ProCreateApi.Services.Email;

namespace ProCreateApi.Services.Inventory;

/// <summary>
/// Composes the inventory-alert digest and emails it to the configured
/// recipients. The "should we send today" decision belongs to the scheduler —
/// this just builds and sends, so the same path serves a manual "send now".
/// </summary>
public class InventoryAlertDigest
{
    private readonly InventoryAlertService _alerts;
    private readonly IEmailSender _email;
    private readonly InventoryAlertSettings _settings;

    public InventoryAlertDigest(
        InventoryAlertService alerts, IEmailSender email, InventoryAlertSettings settings)
    {
        _alerts = alerts;
        _email = email;
        _settings = settings;
    }

    public async Task<(bool sent, string message)> SendAsync(CancellationToken ct = default)
    {
        if (_settings.Recipients.Length == 0)
            return (false, "No recipients configured. Set InventoryAlerts:Recipients.");
        if (!_email.IsConfigured)
            return (false, "Email is not configured. Set the Email section in appsettings.");

        var report = await _alerts.BuildAsync(_settings.ExpiryDays);
        if (report.Counts.Total == 0)
            return (false, "Nothing to report — everything is in date and above reorder level.");

        var subject = $"Inventory alerts — {report.Counts.Total} to review";
        var html = BuildHtml(report);

        var failures = new List<string>();
        foreach (var to in _settings.Recipients)
        {
            var result = await _email.SendAsync(to, subject, html, null, ct);
            if (!result.Sent) failures.Add($"{to}: {result.FailureReason}");
        }

        return failures.Count == 0
            ? (true, $"Digest sent to {_settings.Recipients.Length} recipient(s).")
            : (false, string.Join("; ", failures));
    }

    private static string BuildHtml(InventoryAlertReport r)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#2b2b2b;max-width:640px\">");
        sb.Append("<h2 style=\"margin:0 0 4px\">Inventory alerts</h2>");
        sb.Append($"<p style=\"margin:0 0 16px;color:#6b6b6b\">{r.Counts.Total} item(s) need attention " +
                  $"&middot; expiry window {r.ExpiryDays} days.</p>");

        Section(sb, "Expired batches", r.Expired.Count,
            r.Expired.Select(e => $"{e.ItemName} — batch {Dash(e.BatchNumber)}, " +
                $"{e.QuantityRemaining} {e.Unit}, expired {Math.Abs(e.DaysToExpiry ?? 0)} day(s) ago"), "#c0392b");

        Section(sb, "Expiring soon", r.ExpiringSoon.Count,
            r.ExpiringSoon.Select(e => $"{e.ItemName} — batch {Dash(e.BatchNumber)}, " +
                $"{e.QuantityRemaining} {e.Unit}, in {e.DaysToExpiry} day(s)"), "#b8860b");

        Section(sb, "Out of stock", r.OutOfStock.Count,
            r.OutOfStock.Select(i => $"{i.Name}{Brand(i.BrandName)}"), "#c0392b");

        Section(sb, "Low stock", r.LowStock.Count,
            r.LowStock.Select(i => $"{i.Name}{Brand(i.BrandName)} — {i.CurrentStock} {i.UnitOfMeasure} left " +
                $"(reorder at {i.ReorderLevel})"), "#b8860b");

        sb.Append("<p style=\"margin:18px 0 0;color:#9b9b9b;font-size:12px\">" +
                  "Pro-Create inventory. This is an automated digest.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, int count, IEnumerable<string> lines, string colour)
    {
        if (count == 0) return;
        sb.Append($"<h3 style=\"margin:16px 0 6px;color:{colour}\">{title} ({count})</h3>");
        sb.Append("<ul style=\"margin:0;padding-left:18px\">");
        foreach (var line in lines) sb.Append($"<li style=\"margin:2px 0\">{WebEncode(line)}</li>");
        sb.Append("</ul>");
    }

    private static string Dash(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s;
    private static string Brand(string b) => string.IsNullOrWhiteSpace(b) ? "" : $" ({b})";
    private static string WebEncode(string s) => System.Net.WebUtility.HtmlEncode(s);
}
