namespace ProCreateApi.Services.Inventory;

/// <summary>
/// Controls the emailed inventory-alert digest, bound from the "InventoryAlerts"
/// section of appsettings.
///
/// Off by default: a digest goes to real inboxes on a schedule, so the clinic
/// has to turn it on and name its recipients first. Until then the in-app header
/// bell is the only alert surface, and a manual "send now" is the only way mail
/// goes out.
/// </summary>
public class InventoryAlertSettings
{
    /// <summary>When true, the scheduler sends a digest each day at SendHour.</summary>
    public bool Enabled { get; set; }

    /// <summary>Who the digest goes to. No recipients means nothing is sent.</summary>
    public string[] Recipients { get; set; } = Array.Empty<string>();

    /// <summary>Local hour of day (0–23) the daily check runs.</summary>
    public int SendHour { get; set; } = 7;

    /// <summary>How many days ahead counts as "expiring soon" for the in-app alerts.</summary>
    public int ExpiryDays { get; set; } = 30;

    /// <summary>
    /// Months-before-expiry at which a reminder email goes out — one email each
    /// as a lot crosses each milestone. Defaults to 3, 2 and 1 months.
    /// </summary>
    public int[] ReminderMonths { get; set; } = { 3, 2, 1 };
}
