namespace ProCreateApi.Services.Inventory;

/// <summary>
/// Sends the inventory-alert digest once a day at the configured hour — but only
/// while InventoryAlerts:Enabled is true. Left off, it wakes, sees it is
/// disabled, and goes back to sleep, so the feature ships dormant until the
/// clinic turns it on and names its recipients.
/// </summary>
public class InventoryAlertScheduler : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly InventoryAlertSettings _settings;
    private readonly ILogger<InventoryAlertScheduler> _log;

    public InventoryAlertScheduler(
        IServiceProvider services, InventoryAlertSettings settings, ILogger<InventoryAlertScheduler> log)
    {
        _services = services;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(DelayUntilNextRun(), stoppingToken); }
            catch (TaskCanceledException) { break; }

            if (!_settings.Enabled) continue;

            try
            {
                // The digest touches the database, so it runs in its own scope.
                using var scope = _services.CreateScope();
                var digest = scope.ServiceProvider.GetRequiredService<InventoryAlertDigest>();
                var (_, message) = await digest.SendAsync(stoppingToken);
                _log.LogInformation("Inventory alert digest: {Message}", message);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Inventory alert digest failed");
            }
        }
    }

    /// <summary>Time until the next occurrence of the configured send hour.</summary>
    private TimeSpan DelayUntilNextRun()
    {
        var now = DateTime.Now;
        var next = now.Date.AddHours(Math.Clamp(_settings.SendHour, 0, 23));
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
