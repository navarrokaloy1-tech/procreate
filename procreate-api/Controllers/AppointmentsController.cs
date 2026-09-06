using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AppointmentsController(AppDbContext db) { _db = db; }

    /// <summary>Statuses shown in the calendar legend, in workflow order.</summary>
    private static readonly string[] Statuses =
    {
        "Scheduled", "Confirmed", "CheckedIn", "InProgress",
        "Completed", "Pending", "Cancelled", "NoShow"
    };

    public record AppointmentDto(
        int Id, string AppointmentCode, int PatientId, string PatientName, string PatientCode,
        int DoctorId, string DoctorName, string Service, DateTime ScheduledAt,
        string Type, string Status, string ChiefComplaint, string Notes, bool IsArchived,
        int? BatchId, int QueuePosition);

    /// <summary>
    /// <paramref name="BatchId"/> books into a session block, which sets the
    /// time from the block and puts the patient at the back of its queue. Left
    /// null, the appointment keeps its exact <paramref name="ScheduledAt"/> and
    /// stays off the batch board — walk-ins and legacy bookings work that way.
    /// </summary>
    public record AppointmentWriteRequest(
        int PatientId, int DoctorId, string Service, DateTime ScheduledAt,
        string Type, string Status, string ChiefComplaint, string Notes, int? BatchId);

    /// <summary>
    /// Appointments in a window. Pass `from`/`to` for a calendar month, or
    /// `date` for a single day. `doctorId` scopes the list to one practitioner.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] DateTime? date,
        [FromQuery] int? doctorId, [FromQuery] string? status, [FromQuery] bool includeArchived = false)
    {
        var query = _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .AsQueryable();

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(a => a.ScheduledAt >= day && a.ScheduledAt < day.AddDays(1));
        }
        else
        {
            if (from.HasValue) query = query.Where(a => a.ScheduledAt >= from.Value.Date);
            if (to.HasValue) query = query.Where(a => a.ScheduledAt < to.Value.Date.AddDays(1));
        }

        if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        if (!includeArchived) query = query.Where(a => !a.IsArchived);

        var appointments = await query
            .OrderBy(a => a.ScheduledAt)
            .Select(a => Project(a))
            .ToListAsync();

        return Ok(new { total = appointments.Count, data = appointments });
    }

    /// <summary>Counts by status for the given day — drives the legend badges.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? date, [FromQuery] int? doctorId)
    {
        var day = (date ?? DateTime.Today).Date;

        var query = _db.Appointments
            .Where(a => !a.IsArchived && a.ScheduledAt >= day && a.ScheduledAt < day.AddDays(1));

        if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId.Value);

        var grouped = await query
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            date = day,
            total = grouped.Sum(g => g.Count),
            byStatus = Statuses.ToDictionary(
                s => s,
                s => grouped.FirstOrDefault(g => g.Status == s)?.Count ?? 0)
        });
    }

    [HttpGet("statuses")]
    public IActionResult GetStatuses() => Ok(Statuses);

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.Id == id)
            .Select(a => Project(a))
            .FirstOrDefaultAsync();

        return appointment is null ? NotFound() : Ok(appointment);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentWriteRequest request)
    {
        var (batch, batchError) = await ResolveBatchAsync(request.BatchId, request.PatientId, null);
        if (batchError is not null) return batchError;

        var scheduledAt = ScheduledAtFor(request, batch);

        var validation = await ValidateAsync(request with { ScheduledAt = scheduledAt }, batch is not null);
        if (validation is not null) return validation;

        var appointment = new Appointment
        {
            AppointmentCode = await NextCodeAsync(scheduledAt),
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            Service = string.IsNullOrWhiteSpace(request.Service)
                ? "General Consultation"
                : request.Service.Trim(),
            ScheduledAt = scheduledAt,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Scheduled" : request.Type,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Scheduled" : request.Status,
            ChiefComplaint = request.ChiefComplaint?.Trim() ?? "",
            Notes = request.Notes?.Trim() ?? "",
            BatchId = batch?.Id,
            // Back of the queue: booking order is the default order of service.
            QueuePosition = batch is null ? 0 : NextPosition(batch)
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, await ReloadAsync(appointment.Id));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentWriteRequest request)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound();

        var (batch, batchError) = await ResolveBatchAsync(request.BatchId, request.PatientId, id);
        if (batchError is not null) return batchError;

        var scheduledAt = ScheduledAtFor(request, batch);

        var validation = await ValidateAsync(request with { ScheduledAt = scheduledAt }, batch is not null, id);
        if (validation is not null) return validation;

        // Moving into a different block puts the patient at the back of it; the
        // front desk reorders from the board rather than from this form.
        if (batch is not null && appointment.BatchId != batch.Id)
            appointment.QueuePosition = NextPosition(batch);

        appointment.BatchId = batch?.Id;
        if (batch is null) appointment.QueuePosition = 0;

        appointment.PatientId = request.PatientId;
        appointment.DoctorId = request.DoctorId;
        appointment.Service = string.IsNullOrWhiteSpace(request.Service)
            ? appointment.Service
            : request.Service.Trim();
        appointment.ScheduledAt = scheduledAt;
        appointment.Type = request.Type ?? appointment.Type;
        appointment.Status = request.Status ?? appointment.Status;
        appointment.ChiefComplaint = request.ChiefComplaint?.Trim() ?? "";
        appointment.Notes = request.Notes?.Trim() ?? "";

        await _db.SaveChangesAsync();
        return Ok(await ReloadAsync(id));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] Dictionary<string, string> body)
    {
        if (!body.TryGetValue("status", out var status) || !Statuses.Contains(status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", Statuses)}." });

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound();

        appointment.Status = status;
        await _db.SaveChangesAsync();
        return Ok(await ReloadAsync(id));
    }

    /// <summary>Archive rather than delete, so history stays auditable.</summary>
    [HttpPatch("{id}/archive")]
    public async Task<IActionResult> Archive(int id, [FromBody] Dictionary<string, bool> body)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound();

        appointment.IsArchived = body.TryGetValue("isArchived", out var archived) ? archived : true;
        await _db.SaveChangesAsync();
        return Ok(await ReloadAsync(id));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound();

        _db.Appointments.Remove(appointment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Resolves the requested session block and checks there is room in it.
    /// Returns (null, null) when no block was asked for, which is a valid
    /// booking — it simply keeps its exact time and stays off the board.
    /// </summary>
    private async Task<(AppointmentBatch? Batch, IActionResult? Error)> ResolveBatchAsync(
        int? batchId, int patientId, int? excludeAppointmentId)
    {
        if (batchId is null) return (null, null);

        var batch = await _db.AppointmentBatches
            .Include(b => b.Appointments)
            .FirstOrDefaultAsync(b => b.Id == batchId.Value);

        if (batch is null)
            return (null, BadRequest(new { message = "Selected time block does not exist." }));

        var label = $"{AppointmentBatchesController.Display(batch.StartTime)}"
                  + $"–{AppointmentBatchesController.Display(batch.EndTime)}";

        if (batch.IsClosed)
            return (null, Conflict(new { message = $"The {label} block is closed for bookings." }));

        var occupants = batch.Appointments
            .Where(a => a.Id != excludeAppointmentId && !a.IsArchived)
            .Where(a => !AppointmentBatchesController.ReleasedStatuses.Contains(a.Status))
            .ToList();

        if (occupants.Count >= batch.Capacity)
            return (null, Conflict(new
            {
                message = $"The {label} block is full — it takes {batch.Capacity} patients."
            }));

        if (occupants.Any(a => a.PatientId == patientId))
            return (null, Conflict(new
            {
                message = $"That patient is already booked into the {label} block."
            }));

        return (batch, null);
    }

    /// <summary>A block fixes the time; without one the caller's stands.</summary>
    private static DateTime ScheduledAtFor(AppointmentWriteRequest request, AppointmentBatch? batch) =>
        batch is null
            ? request.ScheduledAt
            : batch.BatchDate.Add(AppointmentBatchesController.ParseTime(batch.StartTime));

    /// <summary>Next free place at the back of a block's queue.</summary>
    private static int NextPosition(AppointmentBatch batch) =>
        batch.Appointments.Count == 0 ? 1 : batch.Appointments.Max(a => a.QueuePosition) + 1;

    /// <summary>
    /// Rejects unknown patients/doctors, out-of-hours slots, and double-booking
    /// the same practitioner at the same time.
    /// </summary>
    private async Task<IActionResult?> ValidateAsync(
        AppointmentWriteRequest request, bool batched = false, int? excludeAppointmentId = null)
    {
        if (request.ScheduledAt == default)
            return BadRequest(new { message = "A date and time is required." });

        if (!await _db.Patients.AnyAsync(p => p.Id == request.PatientId))
            return BadRequest(new { message = "Selected patient does not exist." });

        var doctor = await _db.Doctors
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == request.DoctorId);

        if (doctor is null)
            return BadRequest(new { message = "Selected doctor does not exist." });

        if (!doctor.IsActive)
            return BadRequest(new { message = "Selected doctor is inactive." });

        var day = (int)request.ScheduledAt.DayOfWeek;
        var slot = doctor.Schedules.FirstOrDefault(s => s.DayOfWeek == day);

        if (slot is null || !slot.IsAvailable)
            return BadRequest(new { message = $"Dr. {doctor.LastName} is not available on {request.ScheduledAt.DayOfWeek}s." });

        var time = request.ScheduledAt.ToString("HH:mm");
        if (string.Compare(time, slot.StartTime, StringComparison.Ordinal) < 0 ||
            string.Compare(time, slot.EndTime, StringComparison.Ordinal) > 0)
        {
            return BadRequest(new
            {
                message = $"{time} is outside Dr. {doctor.LastName}'s hours ({slot.StartTime}–{slot.EndTime})."
            });
        }

        // Exact-time double booking only means something for an appointment
        // booked at an exact time. Everyone in a session block shares its start
        // time by design, so this check would reject all but the first of them;
        // capacity and the patient-already-in-this-block check cover those.
        if (batched) return null;

        var clash = await _db.Appointments.AnyAsync(a =>
            a.DoctorId == request.DoctorId &&
            a.ScheduledAt == request.ScheduledAt &&
            a.Id != excludeAppointmentId &&
            a.BatchId == null &&
            !a.IsArchived &&
            a.Status != "Cancelled" && a.Status != "NoShow");

        if (clash)
            return Conflict(new { message = "That doctor already has an appointment at this time." });

        return null;
    }

    private async Task<AppointmentDto?> ReloadAsync(int id) =>
        await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.Id == id)
            .Select(a => Project(a))
            .FirstOrDefaultAsync();

    /// <summary>Flat projection — keeps the Patient/Doctor graph out of the JSON.</summary>
    private static AppointmentDto Project(Appointment a) => new(
        a.Id, a.AppointmentCode, a.PatientId,
        (a.Patient.FirstName + " " + a.Patient.LastName).Trim(),
        a.Patient.PatientCode,
        a.DoctorId,
        ("Dr. " + a.Doctor.FirstName + " " + a.Doctor.LastName).Trim(),
        a.Service, a.ScheduledAt, a.Type, a.Status,
        a.ChiefComplaint, a.Notes, a.IsArchived,
        a.BatchId, a.QueuePosition);

    private async Task<string> NextCodeAsync(DateTime scheduledAt)
    {
        var day = scheduledAt.Date;
        var sameDay = await _db.Appointments
            .CountAsync(a => a.ScheduledAt >= day && a.ScheduledAt < day.AddDays(1));

        return $"APT-{day:yyyyMMdd}-{sameDay + 1:D4}";
    }
}
