using ProCreateApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// The sign-in history, for administrators. Every attempt — successful or not,
/// password or single sign-on, staff or patient — is here, with who, when, from
/// where, and why a failure failed. Admin-only: a login trail is sensitive, and
/// a run of failures is exactly what an administrator needs to notice.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/login-activity")]
public class LoginActivityController : ControllerBase
{
    private readonly AppDbContext _db;

    public LoginActivityController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery] string? result,    // "success" | "failure"
        [FromQuery] string? method,    // "Password" | "SSO"
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var q = _db.LoginActivities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(a => a.UsernameAttempted.ToLower().Contains(s)
                          || a.FullName.ToLower().Contains(s)
                          || a.IpAddress.Contains(s));
        }

        if (string.Equals(result, "success", StringComparison.OrdinalIgnoreCase))
            q = q.Where(a => a.Success);
        else if (string.Equals(result, "failure", StringComparison.OrdinalIgnoreCase))
            q = q.Where(a => !a.Success);

        if (!string.IsNullOrWhiteSpace(method))
            q = q.Where(a => a.Method == method);

        if (from is not null) q = q.Where(a => a.CreatedAt >= from);
        if (to is not null) q = q.Where(a => a.CreatedAt < to.Value.Date.AddDays(1));

        var total = await q.CountAsync();
        var data = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.UsernameAttempted,
                a.FullName,
                a.Role,
                a.Method,
                a.Audience,
                a.Success,
                a.FailureReason,
                a.IpAddress,
                a.UserAgent,
                a.CreatedAt
            })
            .ToListAsync();

        // A quick tally for the header: failed attempts in the last 24 hours,
        // across everything (not just the current filter).
        var since = DateTime.UtcNow.AddHours(-24);
        var failures24h = await _db.LoginActivities.CountAsync(a => !a.Success && a.CreatedAt >= since);

        return Ok(new { total, page, pageSize, failures24h, data });
    }
}
