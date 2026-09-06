using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// The session blocks a day is divided into, and the order patients are seen
/// within each one. Order defaults to the sequence people booked in, and the
/// front desk rearranges it by hand when somebody has not arrived.
/// </summary>
[ApiController]
[Route("api/appointment-batches")]
public class AppointmentBatchesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AppointmentBatchesController(AppDbContext db) { _db = db; }

    /// <summary>Statuses that no longer occupy a seat in the block.</summary>
    internal static readonly string[] ReleasedStatuses = { "Cancelled", "NoShow" };

    public record BatchSlotDto(
        int AppointmentId, string AppointmentCode, int Position,
        int PatientId, string PatientName, string PatientCode,
        string DoctorName, string Service, string Status, string Type,
        bool HasArrived, DateTime BookedAt);

    public record BatchDto(
        int Id, DateTime BatchDate, string StartTime, string EndTime, string Label,
        int Capacity, int Booked, int Remaining, bool IsClosed, List<BatchSlotDto> Slots);

    public record TemplateDto(
        int Id, int DayOfWeek, string StartTime, string EndTime, int Capacity, bool IsActive);

    public record BatchWriteRequest(string StartTime, string EndTime, int Capacity, bool IsClosed);

    public record ArrangeGroup(int BatchId, List<int> AppointmentIds);
    public record ArrangeRequest(DateTime Date, List<ArrangeGroup> Batches);

    // ----------------------------------------------------------
    // Reading a day
    // ----------------------------------------------------------

    /// <summary>
    /// The blocks for a date with their ordered patient lists. Blocks are
    /// created from the weekday template the first time a date is opened.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDay([FromQuery] DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        await EnsureBatchesForAsync(day);

        var batches = await LoadDayAsync(day);
        return Ok(new
        {
            date = day,
            capacity = batches.Sum(b => b.Capacity),
            booked = batches.Sum(b => b.Booked),
            data = batches
        });
    }

    /// <summary>
    /// Blocks that still have room on a date, for the booking form. Returns
    /// only what a new appointment could actually be placed in.
    /// </summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable([FromQuery] DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        await EnsureBatchesForAsync(day);

        var batches = await LoadDayAsync(day);
        var open = batches
            .Where(b => !b.IsClosed && b.Remaining > 0)
            .Select(b => new { b.Id, b.StartTime, b.EndTime, b.Label, b.Capacity, b.Booked, b.Remaining })
            .ToList();

        return Ok(new { date = day, data = open });
    }

    // ----------------------------------------------------------
    // Reordering
    // ----------------------------------------------------------

    /// <summary>
    /// Rewrites the whole day's arrangement in one go — this is what a drag
    /// lands on. Taking the entire day rather than a single block means moving
    /// somebody from the morning to the afternoon is the same operation as
    /// nudging them down one place, and both are applied atomically.
    /// </summary>
    [HttpPost("arrange")]
    public async Task<IActionResult> Arrange([FromBody] ArrangeRequest request)
    {
        var day = request.Date.Date;

        var batches = await _db.AppointmentBatches
            .Where(b => b.BatchDate == day)
            .ToListAsync();

        var appointments = await _db.Appointments
            .Where(a => a.BatchId != null && batches.Select(b => b.Id).Contains(a.BatchId!.Value))
            .ToListAsync();

        // Every appointment currently in the day must appear exactly once in
        // the request. A client working from a stale board would otherwise
        // silently drop whoever was booked in the meantime.
        var submitted = request.Batches.SelectMany(g => g.AppointmentIds).ToList();

        if (submitted.Count != submitted.Distinct().Count())
            return BadRequest(new { message = "The same appointment appears twice in the new order." });

        var known = appointments.Select(a => a.Id).ToHashSet();

        if (!submitted.All(known.Contains))
            return BadRequest(new { message = "The new order refers to an appointment that is not in this day." });

        if (submitted.Count != known.Count)
            return Conflict(new
            {
                message = "The schedule changed while you were rearranging it. Reload the day and try again."
            });

        foreach (var group in request.Batches)
        {
            var batch = batches.FirstOrDefault(b => b.Id == group.BatchId);
            if (batch is null)
                return BadRequest(new { message = "The new order refers to a block that is not in this day." });

            // Cancelled and no-show patients stay listed but stop consuming a
            // seat, so a full block can still take a replacement.
            var occupying = group.AppointmentIds
                .Select(id => appointments.First(a => a.Id == id))
                .Count(a => !ReleasedStatuses.Contains(a.Status));

            if (occupying > batch.Capacity)
                return Conflict(new
                {
                    message = $"The {Display(batch.StartTime)}–{Display(batch.EndTime)} block "
                            + $"holds {batch.Capacity}; that would put {occupying} in it."
                });
        }

        foreach (var group in request.Batches)
        {
            var batch = batches.First(b => b.Id == group.BatchId);

            for (var i = 0; i < group.AppointmentIds.Count; i++)
            {
                var appointment = appointments.First(a => a.Id == group.AppointmentIds[i]);
                appointment.BatchId = batch.Id;
                appointment.QueuePosition = i + 1;

                // Keep the timestamp pointing at the block the patient is now
                // in, so the calendar and the day list stay consistent.
                appointment.ScheduledAt = day.Add(ParseTime(batch.StartTime));
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { date = day, data = await LoadDayAsync(day) });
    }

    // ----------------------------------------------------------
    // Editing one day's blocks
    // ----------------------------------------------------------

    /// <summary>Adjusts a single day's block without touching the template.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] BatchWriteRequest request)
    {
        var batch = await _db.AppointmentBatches
            .Include(b => b.Appointments)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch is null) return NotFound();

        var invalid = ValidateWindow(request.StartTime, request.EndTime, request.Capacity);
        if (invalid is not null) return invalid;

        var occupying = batch.Appointments.Count(a => !ReleasedStatuses.Contains(a.Status));
        if (request.Capacity < occupying)
            return Conflict(new
            {
                message = $"{occupying} patients are already booked into this block, "
                        + $"so it cannot be capped at {request.Capacity}."
            });

        var clash = await _db.AppointmentBatches.AnyAsync(b =>
            b.BatchDate == batch.BatchDate && b.Id != id && b.StartTime == request.StartTime);

        if (clash)
            return Conflict(new { message = $"This day already has a block starting at {Display(request.StartTime)}." });

        var moved = batch.StartTime != request.StartTime;

        batch.StartTime = request.StartTime;
        batch.EndTime = request.EndTime;
        batch.Capacity = request.Capacity;
        batch.IsClosed = request.IsClosed;

        if (moved)
        {
            var start = batch.BatchDate.Add(ParseTime(batch.StartTime));
            foreach (var appointment in batch.Appointments) appointment.ScheduledAt = start;
        }

        await _db.SaveChangesAsync();
        return Ok(new { date = batch.BatchDate, data = await LoadDayAsync(batch.BatchDate) });
    }

    /// <summary>Adds a one-off block to a date — an extra evening session, say.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromQuery] DateTime date, [FromBody] BatchWriteRequest request)
    {
        var day = date.Date;

        var invalid = ValidateWindow(request.StartTime, request.EndTime, request.Capacity);
        if (invalid is not null) return invalid;

        if (await _db.AppointmentBatches.AnyAsync(b => b.BatchDate == day && b.StartTime == request.StartTime))
            return Conflict(new { message = $"This day already has a block starting at {Display(request.StartTime)}." });

        _db.AppointmentBatches.Add(new AppointmentBatch
        {
            BatchDate = day,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Capacity = request.Capacity,
            IsClosed = request.IsClosed
        });

        await _db.SaveChangesAsync();
        return Ok(new { date = day, data = await LoadDayAsync(day) });
    }

    /// <summary>Removes an empty block. One with patients has to be emptied first.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var batch = await _db.AppointmentBatches
            .Include(b => b.Appointments)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch is null) return NotFound();

        if (batch.Appointments.Count > 0)
            return Conflict(new
            {
                message = "Move or cancel the patients in this block before removing it."
            });

        var day = batch.BatchDate;
        _db.AppointmentBatches.Remove(batch);
        await _db.SaveChangesAsync();

        return Ok(new { date = day, data = await LoadDayAsync(day) });
    }

    // ----------------------------------------------------------
    // The weekly template
    // ----------------------------------------------------------

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _db.AppointmentBatchTemplates
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime)
            .Select(t => new TemplateDto(t.Id, t.DayOfWeek, t.StartTime, t.EndTime, t.Capacity, t.IsActive))
            .ToListAsync();

        return Ok(new { total = templates.Count, data = templates });
    }

    /// <summary>
    /// Replaces the template for one weekday. Days already materialised keep
    /// the blocks they were given — this only changes what future days inherit.
    /// </summary>
    [HttpPut("templates/{dayOfWeek:int}")]
    public async Task<IActionResult> ReplaceTemplates(int dayOfWeek, [FromBody] List<BatchWriteRequest> rows)
    {
        if (dayOfWeek is < 0 or > 6)
            return BadRequest(new { message = "Day of week must be 0 (Sunday) through 6 (Saturday)." });

        foreach (var row in rows)
        {
            var invalid = ValidateWindow(row.StartTime, row.EndTime, row.Capacity);
            if (invalid is not null) return invalid;
        }

        var starts = rows.Select(r => r.StartTime).ToList();
        if (starts.Count != starts.Distinct().Count())
            return BadRequest(new { message = "Two blocks cannot start at the same time." });

        var existing = await _db.AppointmentBatchTemplates
            .Where(t => t.DayOfWeek == dayOfWeek)
            .ToListAsync();

        _db.AppointmentBatchTemplates.RemoveRange(existing);
        await _db.SaveChangesAsync();

        _db.AppointmentBatchTemplates.AddRange(rows.Select(r => new AppointmentBatchTemplate
        {
            DayOfWeek = dayOfWeek,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            Capacity = r.Capacity,
            IsActive = !r.IsClosed
        }));

        await _db.SaveChangesAsync();
        return await GetTemplates();
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    /// <summary>
    /// Copies the weekday template onto a date, once. Existing blocks are left
    /// alone, so a day that has been adjusted by hand is never reset.
    /// </summary>
    private async Task EnsureBatchesForAsync(DateTime day)
    {
        if (await _db.AppointmentBatches.AnyAsync(b => b.BatchDate == day)) return;

        var templates = await _db.AppointmentBatchTemplates
            .Where(t => t.DayOfWeek == (int)day.DayOfWeek && t.IsActive)
            .OrderBy(t => t.StartTime)
            .ToListAsync();

        if (templates.Count == 0) return;

        _db.AppointmentBatches.AddRange(templates.Select(t => new AppointmentBatch
        {
            BatchDate = day,
            StartTime = t.StartTime,
            EndTime = t.EndTime,
            Capacity = t.Capacity,
            TemplateId = t.Id
        }));

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another request materialised the same day first; the unique index
            // on (BatchDate, StartTime) caught it. Its rows are just as good.
            foreach (var entry in _db.ChangeTracker.Entries<AppointmentBatch>().ToList())
                entry.State = EntityState.Detached;
        }
    }

    private async Task<List<BatchDto>> LoadDayAsync(DateTime day)
    {
        var batches = await _db.AppointmentBatches
            .Where(b => b.BatchDate == day)
            .OrderBy(b => b.StartTime)
            .ToListAsync();

        var ids = batches.Select(b => b.Id).ToList();

        var appointments = await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.BatchId != null && ids.Contains(a.BatchId!.Value) && !a.IsArchived)
            .OrderBy(a => a.QueuePosition).ThenBy(a => a.CreatedAt)
            .ToListAsync();

        return batches.Select(b =>
        {
            // Position is the place in the queue rather than the stored sort
            // key, so cancelling the second patient renumbers the rest instead
            // of leaving the board reading 1, 3, 4. Cancellations and no-shows
            // take no number at all — they are not waiting to be seen, and
            // giving them one would make the wrong patient look next.
            var seen = 0;

            var slots = appointments
                .Where(a => a.BatchId == b.Id)
                .Select(a => new BatchSlotDto(
                    a.Id, a.AppointmentCode,
                    ReleasedStatuses.Contains(a.Status) ? 0 : ++seen,
                    a.PatientId,
                    (a.Patient.FirstName + " " + a.Patient.LastName).Trim(),
                    a.Patient.PatientCode,
                    ("Dr. " + a.Doctor.FirstName + " " + a.Doctor.LastName).Trim(),
                    a.Service, a.Status, a.Type,
                    a.Status is "CheckedIn" or "InProgress" or "Completed",
                    a.CreatedAt))
                .ToList();

            var booked = slots.Count(s => !ReleasedStatuses.Contains(s.Status));

            return new BatchDto(
                b.Id, b.BatchDate, b.StartTime, b.EndTime,
                $"{Display(b.StartTime)} – {Display(b.EndTime)}",
                b.Capacity, booked, Math.Max(0, b.Capacity - booked), b.IsClosed, slots);
        }).ToList();
    }

    private BadRequestObjectResult? ValidateWindow(string start, string end, int capacity)
    {
        if (!TimeSpan.TryParse(start, out var from) || !TimeSpan.TryParse(end, out var to))
            return BadRequest(new { message = "Times must be given as HH:mm." });

        if (to <= from)
            return BadRequest(new { message = "A block has to end after it starts." });

        if (capacity < 1)
            return BadRequest(new { message = "A block needs room for at least one patient." });

        return null;
    }

    /// <summary>"09:00" as a TimeSpan; falls back to midnight on anything odd.</summary>
    internal static TimeSpan ParseTime(string value) =>
        TimeSpan.TryParse(value, out var parsed) ? parsed : TimeSpan.Zero;

    /// <summary>"13:00" as "1:00 PM", for labels.</summary>
    internal static string Display(string value) =>
        TimeSpan.TryParse(value, out var parsed)
            ? DateTime.Today.Add(parsed).ToString("h:mm tt")
            : value;
}
