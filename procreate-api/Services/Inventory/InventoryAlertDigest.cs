using System.Text;
using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Services.Inventory;

/// <summary>
/// Emails expiry reminders to the configured recipients as each received lot
/// crosses a milestone before it expires — three months out, two months out,
/// one month out — once per milestone. The daily scheduler calls this; it only
/// mails when a lot has newly crossed a milestone, so in practice a lot triggers
/// at most one mail a month. The same path serves a manual "send now".
/// </summary>
public class InventoryAlertDigest
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly InventoryAlertSettings _settings;

    public InventoryAlertDigest(AppDbContext db, IEmailSender email, InventoryAlertSettings settings)
    {
        _db = db;
        _email = email;
        _settings = settings;
    }

    private record Due(StockBatch Batch, int Milestone);

    public async Task<(bool sent, string message)> SendAsync(CancellationToken ct = default)
    {
        if (_settings.Recipients.Length == 0)
            return (false, "No recipients configured. Set InventoryAlerts:Recipients.");
        if (!_email.IsConfigured)
            return (false, "Email is not configured. Set the Email section in appsettings.");

        // Largest first (3, 2, 1), so the nearest milestone a lot sits in wins.
        var months = (_settings.ReminderMonths ?? new[] { 3, 2, 1 })
            .Where(m => m > 0).Distinct().OrderByDescending(m => m).ToArray();
        if (months.Length == 0)
            return (false, "No reminder milestones configured.");

        var today = DateTime.Today;
        var batches = await _db.StockBatches
            .Include(b => b.InventoryItem)
            .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate != null)
            .ToListAsync(ct);

        var due = new List<Due>();
        foreach (var b in batches)
        {
            var milestone = MilestoneFor(b.ExpiryDate!.Value.Date, today, months);
            // Milestones only get nearer over time (3 -> 2 -> 1), so a change from
            // the last one emailed means a new milestone has been reached.
            if (milestone != 0 && milestone != b.ExpiryNoticeStage)
                due.Add(new Due(b, milestone));
        }

        if (due.Count == 0)
            return (false, "No lots have reached a reminder milestone.");

        var subject = $"Expiry reminder — {due.Count} {(due.Count == 1 ? "item" : "items")} to review";
        var html = BuildHtml(due, today);

        var failures = new List<string>();
        foreach (var to in _settings.Recipients)
        {
            var result = await _email.SendAsync(to, subject, html, null, ct);
            if (!result.Sent) failures.Add($"{to}: {result.FailureReason}");
        }

        if (failures.Count > 0)
            return (false, string.Join("; ", failures));

        // Only after a successful send, so a mail failure is retried next run.
        foreach (var d in due) d.Batch.ExpiryNoticeStage = d.Milestone;
        await _db.SaveChangesAsync(ct);

        return (true, $"Sent {due.Count} reminder(s) to {_settings.Recipients.Length} recipient(s).");
    }

    /// <summary>
    /// The nearest milestone (in months) the lot currently sits within, or 0 if
    /// it is further out than the largest milestone or already expired. Uses real
    /// calendar months rather than a day count.
    /// </summary>
    private static int MilestoneFor(DateTime expiry, DateTime today, int[] monthsDescending)
    {
        if (today >= expiry) return 0; // expired — handled by the in-app alerts
        // Ascending so the closest milestone the date has entered is returned.
        foreach (var m in monthsDescending.OrderBy(x => x))
            if (today >= expiry.AddMonths(-m))
                return m;
        return 0;
    }

    private static string BuildHtml(List<Due> due, DateTime today)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#2b2b2b;max-width:640px\">");
        sb.Append("<h2 style=\"margin:0 0 4px\">Inventory expiry reminder</h2>");
        sb.Append("<p style=\"margin:0 0 16px;color:#6b6b6b\">The following stock is approaching its expiry date.</p>");

        foreach (var months in due.Select(d => d.Milestone).Distinct().OrderByDescending(m => m))
        {
            var rows = due.Where(d => d.Milestone == months).OrderBy(d => d.Batch.ExpiryDate).ToList();
            sb.Append($"<h3 style=\"margin:16px 0 6px;color:#b8860b\">{months} month{(months == 1 ? "" : "s")} before expiry</h3>");
            sb.Append("<ul style=\"margin:0;padding-left:18px\">");
            foreach (var d in rows)
            {
                var b = d.Batch;
                var dateText = b.ExpiryDate!.Value.ToString("MMM d, yyyy");
                var batchText = string.IsNullOrWhiteSpace(b.BatchNumber) ? "" : $" (batch {WebEncode(b.BatchNumber)})";
                sb.Append($"<li style=\"margin:2px 0\">{WebEncode(b.InventoryItem.Name)}{batchText} — " +
                          $"{b.QuantityRemaining} {WebEncode(b.InventoryItem.UnitOfMeasure)} left, expires {dateText}</li>");
            }
            sb.Append("</ul>");
        }

        sb.Append("<p style=\"margin:18px 0 0;color:#9b9b9b;font-size:12px\">" +
                  "Pro-Create inventory. This is an automated reminder.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string WebEncode(string s) => System.Net.WebUtility.HtmlEncode(s);
}
