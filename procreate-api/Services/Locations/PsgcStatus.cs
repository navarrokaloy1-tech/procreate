namespace ProCreateApi.Services.Locations;

/// <summary>
/// Shared fetch status for the PSGC lookup.
///
/// Registered as a singleton on purpose: AddHttpClient registers its typed
/// client as transient, so per-instance fields would reset on every request and
/// the /locations/source endpoint would always report "offline".
/// </summary>
public class PsgcStatus
{
    private readonly object _gate = new();

    private DateTime? _lastSuccess;
    private string? _lastError;

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _lastSuccess = DateTime.UtcNow;
            _lastError = null;
        }
    }

    public void RecordFailure(string message)
    {
        lock (_gate)
        {
            _lastError = message;
        }
    }

    public (DateTime? LastSuccess, string? LastError) Read()
    {
        lock (_gate)
        {
            return (_lastSuccess, _lastError);
        }
    }
}
