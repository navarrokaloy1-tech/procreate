using ProCreateApi.Data;
using ProCreateApi.Models;

namespace ProCreateApi.Services.Auth;

/// <summary>
/// Records every sign-in attempt — win or lose, password or SSO — so an
/// administrator can review the login history. Writing is best-effort: an
/// audit failure must never stop someone signing in, so it swallows errors.
/// </summary>
public class LoginAudit
{
    private readonly AppDbContext _db;

    public LoginAudit(AppDbContext db) { _db = db; }

    public Task RecordSuccessAsync(HttpContext? http, string method, string audience,
        string usernameAttempted, int userId, string fullName, string role) =>
        WriteAsync(http, method, audience, usernameAttempted, true, string.Empty, userId, fullName, role);

    public Task RecordFailureAsync(HttpContext? http, string method, string audience,
        string usernameAttempted, string reason) =>
        WriteAsync(http, method, audience, usernameAttempted, false, reason, null, string.Empty, string.Empty);

    private async Task WriteAsync(HttpContext? http, string method, string audience,
        string usernameAttempted, bool success, string reason, int? userId, string fullName, string role)
    {
        try
        {
            _db.LoginActivities.Add(new LoginActivity
            {
                UsernameAttempted = (usernameAttempted ?? string.Empty).Trim(),
                UserId = userId,
                FullName = fullName ?? string.Empty,
                Role = role ?? string.Empty,
                Method = method,
                Audience = audience,
                Success = success,
                FailureReason = reason ?? string.Empty,
                IpAddress = ClientIp(http),
                UserAgent = http?.Request.Headers.UserAgent.ToString() ?? string.Empty,
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Auditing is never allowed to break sign-in.
        }
    }

    private static string ClientIp(HttpContext? http)
    {
        if (http is null) return string.Empty;
        // Respect a proxy's forwarded address when present, else the socket.
        var forwarded = http.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();
        return http.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }
}
