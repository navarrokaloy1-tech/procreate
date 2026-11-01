using System.Text.Json;
using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Appointments;
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
        int Booked, int TotalMinutes, int UsedMinutes, int RemainingMinutes,
        int DefaultServiceMinutes, Dictionary<string, int> ServiceDurations,
        bool IsClosed, List<BatchSlotDto> Slots);

    public record TemplateDto(
        int Id, int DayOfWeek, string StartTime, string EndTime, int Capacity, bool IsActive);

    public record BatchWriteRequest(
        string StartTime, string EndTime, bool IsClosed,
        Dictionary<string, int>? ServiceDurations, int? DefaultServiceMinutes);

    public record ArrangeGroup(int BatchId, List<int> AppointmentIds);
    public record ArrangeRequest(DateTime Date, List<ArrangeGroup> Batches);

    /// <summary>One block's new window, for the cascade save.</summary>
    public record BlockTimeUpdate(int Id, string StartTime, string EndTime);
    public record DayTimesRequest(DateTime Date, List<BlockTimeUpdate> Blocks);

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
            totalMinutes = batches.Sum(b => b.TotalMinutes),
            usedMinutes = batches.Sum(b => b.UsedMinutes),
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
            .Where(b => !b.IsClosed && b.RemainingMinutes > 0)
            .Select(b => new
            {
                b.Id, b.StartTime, b.EndTime, b.Label, b.Booked,
                b.TotalMinutes, b.UsedMinutes, b.RemainingMinutes,
                b.DefaultServiceMinutes, b.ServiceDurations
            })
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

        var serviceDefaults = await ServiceDefaultsAsync();

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

            // Cancelled and no-show patients stay listed but stop consuming
            // time, so a full block can still take a replacement.
            var usedMinutes = group.AppointmentIds
                .Select(id => appointments.First(a => a.Id == id))
                .Where(a => !ReleasedStatuses.Contains(a.Status))
                .Sum(a => BatchBooking.MinutesFor(batch, a.Service, serviceDefaults));

            var totalMinutes = BatchBooking.TotalMinutes(batch);

            if (usedMinutes > totalMinutes)
                return Conflict(new
                {
                    message = $"The {Display(batch.StartTime)}–{Display(batch.EndTime)} block "
                            + $"holds {totalMinutes} min; that arrangement needs {usedMinutes}."
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

        var invalid = ValidateWindow(request.StartTime, request.EndTime);
        if (invalid is not null) return invalid;

        var clash = await _db.AppointmentBatches.AnyAsync(b =>
            b.BatchDate == batch.BatchDate && b.Id != id && b.StartTime == request.StartTime);

        if (clash)
            return Conflict(new { message = $"This day already has a block starting at {Display(request.StartTime)}." });

        var moved = batch.StartTime != request.StartTime;

        batch.StartTime = request.StartTime;
        batch.EndTime = request.EndTime;
        batch.IsClosed = request.IsClosed;
        ApplyDurations(batch, request);

        // A block cannot be shortened below the time its patients already need.
        var used = BatchBooking.UsedMinutes(batch, await ServiceDefaultsAsync());
        var total = BatchBooking.TotalMinutes(batch);
        if (used > total)
            return Conflict(new
            {
                message = $"The patients already booked here need {used} min, "
                        + $"but this block would only run {total}."
            });

        if (moved)
        {
            var start = batch.BatchDate.Add(ParseTime(batch.StartTime));
            foreach (var appointment in batch.Appointments) appointment.ScheduledAt = start;
        }

        await _db.SaveChangesAsync();
        return Ok(new { date = batch.BatchDate, data = await LoadDayAsync(batch.BatchDate) });
    }

    /// <summary>
    /// Saves several blocks' windows in one go — what the cascade "auto-adjust"
    /// lands on, where editing one block shifts the ones after it. Atomic so a
    /// mid-shift state (two blocks momentarily sharing a start) never persists.
    /// </summary>
    [HttpPut("day")]
    public async Task<IActionResult> UpdateDayTimes([FromBody] DayTimesRequest request)
    {
        var day = request.Date.Date;

        var batches = await _db.AppointmentBatches
            .Include(b => b.Appointments)
            .Where(b => b.BatchDate == day)
            .ToListAsync();

        foreach (var row in request.Blocks)
        {
            var invalid = ValidateWindow(row.StartTime, row.EndTime);
            if (invalid is not null) return invalid;
        }

        var starts = request.Blocks.Select(r => r.StartTime).ToList();
        if (starts.Count != starts.Distinct().Count())
            return BadRequest(new { message = "Two blocks cannot start at the same time." });

        var serviceDefaults = await ServiceDefaultsAsync();

        foreach (var row in request.Blocks)
        {
            var batch = batches.FirstOrDefault(b => b.Id == row.Id);
            if (batch is null)
                return BadRequest(new { message = "A block in the request is not part of this day." });

            batch.StartTime = row.StartTime;
            batch.EndTime = row.EndTime;

            if (BatchBooking.UsedMinutes(batch, serviceDefaults) > BatchBooking.TotalMinutes(batch))
                return Conflict(new
                {
                    message = $"The {Display(row.StartTime)}–{Display(row.EndTime)} block is too short "
                            + "for the patients already in it."
                });

            var start = day.Add(ParseTime(batch.StartTime));
            foreach (var appointment in batch.Appointments) appointment.ScheduledAt = start;
        }

        await _db.SaveChangesAsync();
        return Ok(new { date = day, data = await LoadDayAsync(day) });
    }

    /// <summary>Adds a one-off block to a date — an extra evening session, say.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromQuery] DateTime date, [FromBody] BatchWriteRequest request)
    {
        var day = date.Date;

        var invalid = ValidateWindow(request.StartTime, request.EndTime);
        if (invalid is not null) return invalid;

        if (await _db.AppointmentBatches.AnyAsync(b => b.BatchDate == day && b.StartTime == request.StartTime))
            return Conflict(new { message = $"This day already has a block starting at {Display(request.StartTime)}." });

        var batch = new AppointmentBatch
        {
            BatchDate = day,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsClosed = request.IsClosed
        };
        ApplyDurations(batch, request);
        _db.AppointmentBatches.Add(batch);

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
            var invalid = ValidateWindow(row.StartTime, row.EndTime);
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

        _db.AppointmentBatchTemplates.AddRange(rows.Select(r =>
        {
            var (json, defaultMinutes) = NormalizeDurations(r);
            return new AppointmentBatchTemplate
            {
                DayOfWeek = dayOfWeek,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                ServiceDurationsJson = json,
                DefaultServiceMinutes = defaultMinutes,
                IsActive = !r.IsClosed
            };
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
            ServiceDurationsJson = t.ServiceDurationsJson,
            DefaultServiceMinutes = t.DefaultServiceMinutes,
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
        var serviceDefaults = await ServiceDefaultsAsync();

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

            var totalMinutes = BatchBooking.TotalMinutes(b);
            var usedMinutes = slots
                .Where(s => !ReleasedStatuses.Contains(s.Status))
                .Sum(s => BatchBooking.MinutesFor(b, s.Service, serviceDefaults));

            return new BatchDto(
                b.Id, b.BatchDate, b.StartTime, b.EndTime,
                $"{Display(b.StartTime)} – {Display(b.EndTime)}",
                booked, totalMinutes, usedMinutes, Math.Max(0, totalMinutes - usedMinutes),
                b.DefaultServiceMinutes, BatchBooking.Durations(b), b.IsClosed, slots);
        }).ToList();
    }

    /// <summary>Clinic-wide default minutes per service name, from the catalogue.</summary>
    private async Task<Dictionary<string, int>> ServiceDefaultsAsync()
    {
        var rows = await _db.Products.AsNoTracking()
            .Select(p => new { p.Name, p.DurationMinutes })
            .ToListAsync();

        var map = new Dictionary<string, int>();
        foreach (var row in rows)
            if (!string.IsNullOrWhiteSpace(row.Name)) map[row.Name] = row.DurationMinutes;
        return map;
    }

    private BadRequestObjectResult? ValidateWindow(string start, string end)
    {
        if (!TimeSpan.TryParse(start, out var from) || !TimeSpan.TryParse(end, out var to))
            return BadRequest(new { message = "Times must be given as HH:mm." });

        if (to <= from)
            return BadRequest(new { message = "A block has to end after it starts." });

        return null;
    }

    /// <summary>Writes a request's service→minutes map onto a block.</summary>
    private static void ApplyDurations(AppointmentBatch batch, BatchWriteRequest request)
    {
        var (json, defaultMinutes) = NormalizeDurations(request);
        batch.ServiceDurationsJson = json;
        batch.DefaultServiceMinutes = defaultMinutes;
    }

    /// <summary>Drops blank/non-positive durations and serialises; default is 15 min.</summary>
    private static (string Json, int Default) NormalizeDurations(BatchWriteRequest request)
    {
        var map = (request.ServiceDurations ?? new())
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
            .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value);

        var defaultMinutes = request.DefaultServiceMinutes is > 0 ? request.DefaultServiceMinutes.Value : 15;
        return (JsonSerializer.Serialize(map), defaultMinutes);
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
