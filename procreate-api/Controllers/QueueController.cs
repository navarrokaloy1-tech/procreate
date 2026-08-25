using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// Reception queue: hands out per-day ticket numbers to walk-ins and tracks
/// whether each has been called.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QueueController : ControllerBase
{
    private readonly AppDbContext _db;

    public QueueController(AppDbContext db) { _db = db; }

    public record QueueEntryDto(
        int Id, int QueueNumber, string PatientName, int? PatientId,
        string Status, DateTime AddedAt, DateTime? CalledAt);

    public record AddToQueueRequest(string PatientName, int? PatientId);

    /// <summary>Today's queue (or the given date), oldest ticket first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetQueue([FromQuery] DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;

        var entries = await _db.QueueEntries
            .Where(q => q.QueueDate == day)
            .OrderBy(q => q.QueueNumber)
            .Select(q => new QueueEntryDto(
                q.Id, q.QueueNumber, q.PatientName, q.PatientId,
                q.Status, q.AddedAt, q.CalledAt))
            .ToListAsync();

        return Ok(new
        {
            date = day,
            waiting = entries.Count(e => e.Status == "Waiting"),
            called = entries.Count(e => e.Status == "Called"),
            served = entries.Count(e => e.Status == "Served"),
            data = entries
        });
    }

    /// <summary>Issue the next ticket number for today.</summary>
    [HttpPost]
    public async Task<IActionResult> AddToQueue([FromBody] AddToQueueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PatientName))
            return BadRequest(new { message = "Patient name or identifier is required." });

        var day = DateTime.Today;

        // Numbers restart each day. Retry on the unique (date, number) index so
        // two receptionists clicking at once can't be handed the same ticket.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var lastNumber = await _db.QueueEntries
                .Where(q => q.QueueDate == day)
                .MaxAsync(q => (int?)q.QueueNumber) ?? 0;

            var entry = new QueueEntry
            {
                QueueNumber = lastNumber + 1,
                QueueDate = day,
                PatientName = request.PatientName.Trim(),
                PatientId = request.PatientId,
                Status = "Waiting",
                AddedAt = DateTime.Now
            };

            _db.QueueEntries.Add(entry);
            try
            {
                await _db.SaveChangesAsync();
                return Ok(new QueueEntryDto(
                    entry.Id, entry.QueueNumber, entry.PatientName, entry.PatientId,
                    entry.Status, entry.AddedAt, entry.CalledAt));
            }
            catch (DbUpdateException)
            {
                _db.Entry(entry).State = EntityState.Detached;
            }
        }

        return Conflict(new { message = "Could not allocate a queue number. Please try again." });
    }

    /// <summary>Advance a ticket's state (Waiting → Called → Served, or Skipped).</summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] Dictionary<string, string> body)
    {
        var allowed = new[] { "Waiting", "Called", "Served", "Skipped" };

        if (!body.TryGetValue("status", out var status) || !allowed.Contains(status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", allowed)}." });

        var entry = await _db.QueueEntries.FindAsync(id);
        if (entry is null) return NotFound();

        entry.Status = status;
        if (status == "Called" && entry.CalledAt is null) entry.CalledAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return Ok(new QueueEntryDto(
            entry.Id, entry.QueueNumber, entry.PatientName, entry.PatientId,
            entry.Status, entry.AddedAt, entry.CalledAt));
    }

    /// <summary>Call the next waiting ticket — the display board's primary action.</summary>
    [HttpPost("call-next")]
    public async Task<IActionResult> CallNext()
    {
        var day = DateTime.Today;

        var next = await _db.QueueEntries
            .Where(q => q.QueueDate == day && q.Status == "Waiting")
            .OrderBy(q => q.QueueNumber)
            .FirstOrDefaultAsync();

        if (next is null) return NotFound(new { message = "No one is waiting." });

        next.Status = "Called";
        next.CalledAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new QueueEntryDto(
            next.Id, next.QueueNumber, next.PatientName, next.PatientId,
            next.Status, next.AddedAt, next.CalledAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.QueueEntries.FindAsync(id);
        if (entry is null) return NotFound();

        _db.QueueEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
