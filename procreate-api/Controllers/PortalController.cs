using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ProCreateApi.Controllers;

/// <summary>
/// What a patient can see of their own record.
///
/// Every route resolves the patient from the bearer token rather than taking
/// an id from the URL. That is deliberate: an id parameter would have to be
/// checked against the caller on every action, and one missed check would
/// hand over somebody else's chart.
/// </summary>
[ApiController]
[Authorize(Roles = AuthController.PatientRole)]
[Route("api/[controller]")]
public class PortalController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BatchBooking _booking;

    public PortalController(AppDbContext db, BatchBooking booking)
    {
        _db = db;
        _booking = booking;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var patientId = CallerId();
        if (patientId is null) return Unauthorized();

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient is null) return NotFound();

        var allergies = await _db.PatientAllergies
            .Where(a => a.PatientId == patientId)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Substance, a.Severity, a.Reaction })
            .ToListAsync();

        var medications = await _db.PatientMedications
            .Where(m => m.PatientId == patientId)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Name, m.Dosage, m.Frequency })
            .ToListAsync();

        var vitals = await _db.VitalSignRecords
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.RecordedAt)
            .Take(5)
            .Select(v => new
            {
                v.RecordedAt, v.SystolicBp, v.DiastolicBp,
                v.HeartRate, v.RespiratoryRate, v.TemperatureC
            })
            .ToListAsync();

        // Released only. A reading still being entered or waiting to be read
        // is not a result yet, and showing one would have patients acting on
        // numbers no clinician has signed off.
        var results = await _db.LabOrders
            .Include(o => o.Visit)
            .Include(o => o.LabTest).ThenInclude(t => t.Category)
            .Include(o => o.Results).ThenInclude(r => r.TestParameter)
            .Where(o => o.Visit.PatientId == patientId && o.Status == "Released")
            .OrderByDescending(o => o.ReleasedAt)
            .ToListAsync();

        var appointments = await _db.Appointments
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId && !a.IsArchived)
            .OrderBy(a => a.ScheduledAt)
            .Select(a => new
            {
                a.AppointmentCode,
                a.Service,
                a.ScheduledAt,
                a.Status,
                doctor = a.Doctor.FirstName + " " + a.Doctor.LastName
            })
            .ToListAsync();

        var documents = await _db.PatientDocuments
            .Where(d => d.PatientId == patientId)
            .OrderByDescending(d => d.ResultDate)
            .Select(d => new { d.Id, d.Department, d.FileName, d.SizeBytes, d.ResultDate })
            .ToListAsync();

        return Ok(new
        {
            patient = new
            {
                patient.PatientCode,
                fullName = string.Join(' ', new[] { patient.FirstName, patient.MiddleName, patient.LastName }
                    .Where(part => !string.IsNullOrWhiteSpace(part))),
                patient.DateOfBirth,
                age = LabController.AgeOn(patient.DateOfBirth, DateTime.Today),
                patient.Gender,
                patient.BloodType,
                patient.ContactNumber,
                patient.Email
            },
            allergies,
            medications,
            vitals,
            appointments,
            documents,
            results = results.Select(o => new
            {
                orderCode = $"{o.LabTest.Category.Department[..2].ToUpper()}-{o.Id:D4}",
                o.LabTest.Name,
                department = o.LabTest.Category.Department,
                releasedAt = o.ReleasedAt,
                o.IsAbnormal,
                o.NarrativeFindings,
                parameters = o.Results.Select(r => new
                {
                    parameter = r.TestParameter.Name,
                    r.Value,
                    r.TestParameter.Unit,
                    reference = r.TestParameter.ReferenceRange,
                    r.Flag
                })
            })
        });
    }

    // ----------------------------------------------------------
    // Booking
    // ----------------------------------------------------------

    public record BookRequest(int BatchId, int DoctorId, string? Service, string? Notes);

    /// <summary>Services a patient may book for themselves.</summary>
    private static readonly string[] BookableServices =
    {
        "General Consultation", "Follow-up Consultation", "Prenatal Check-up",
        "Fertility Consultation", "Ultrasound"
    };

    /// <summary>
    /// Which days in a month can be booked, and the doctors and services to
    /// choose from. Drives the calendar, so it answers for the whole month in
    /// one call rather than a request per day.
    /// </summary>
    [HttpGet("booking/month")]
    public async Task<IActionResult> GetBookingMonth([FromQuery] int year, [FromQuery] int month)
    {
        if (CallerId() is null) return Unauthorized();

        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest(new { message = "Give a valid year and month." });

        var first = new DateTime(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        // Nothing in the past, and nothing today either: a same-day slot cannot
        // be honoured through a portal that nobody is watching.
        var earliest = DateTime.Today.AddDays(1);

        var templates = await _db.AppointmentBatchTemplates
            .Where(t => t.IsActive)
            .ToListAsync();

        var existing = await _db.AppointmentBatches
            .Include(b => b.Appointments)
            .Where(b => b.BatchDate >= first && b.BatchDate <= last)
            .ToListAsync();

        var days = new List<object>();

        for (var day = first; day <= last; day = day.AddDays(1))
        {
            if (day < earliest) continue;

            var materialised = existing.Where(b => b.BatchDate == day).ToList();

            // A day nobody has opened yet still has whatever its weekday
            // template would give it, and every seat free.
            var openSeats = materialised.Count > 0
                ? materialised
                    .Where(b => !b.IsClosed)
                    .Sum(b => Math.Max(0, b.Capacity - BatchBooking.Occupants(b).Count()))
                : templates.Where(t => t.DayOfWeek == (int)day.DayOfWeek).Sum(t => t.Capacity);

            if (openSeats > 0) days.Add(new { date = day, openSeats });
        }

        var doctors = await _db.Doctors
            .Where(d => d.IsActive)
            .OrderBy(d => d.LastName)
            .Select(d => new
            {
                d.Id,
                name = ("Dr. " + d.FirstName + " " + d.LastName).Trim(),
                d.Specialty
            })
            .ToListAsync();

        return Ok(new { year, month, days, doctors, services = BookableServices });
    }

    /// <summary>The blocks with room on one day.</summary>
    [HttpGet("booking/day")]
    public async Task<IActionResult> GetBookingDay([FromQuery] DateTime date)
    {
        var patientId = CallerId();
        if (patientId is null) return Unauthorized();

        var day = date.Date;
        if (day <= DateTime.Today)
            return BadRequest(new { message = "Choose a date from tomorrow onwards." });

        await _booking.EnsureBatchesForAsync(day);

        var batches = await _db.AppointmentBatches
            .Include(b => b.Appointments)
            .Where(b => b.BatchDate == day)
            .OrderBy(b => b.StartTime)
            .ToListAsync();

        var slots = batches.Select(b =>
        {
            var occupants = BatchBooking.Occupants(b).ToList();
            return new
            {
                b.Id,
                b.StartTime,
                b.EndTime,
                label = BatchBooking.Label(b),
                remaining = Math.Max(0, b.Capacity - occupants.Count),
                b.IsClosed,
                // Shown as taken rather than hidden, so it is clear why a block
                // they can see is not offered.
                alreadyBooked = occupants.Any(a => a.PatientId == patientId)
            };
        }).ToList();

        return Ok(new { date = day, data = slots });
    }

    /// <summary>
    /// Books the caller into a block. The patient is the token holder — a
    /// patient id in the body would be a request to book somebody else.
    /// </summary>
    [HttpPost("me/appointments")]
    public async Task<IActionResult> Book([FromBody] BookRequest request)
    {
        var patientId = CallerId();
        if (patientId is null) return Unauthorized();

        var patient = await _db.Patients.FindAsync(patientId.Value);
        if (patient is null) return Unauthorized();

        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == request.DoctorId && d.IsActive);
        if (doctor is null)
            return BadRequest(new { message = "Choose a doctor from the list." });

        var service = BookableServices.Contains(request.Service)
            ? request.Service!
            : BookableServices[0];

        var batch = await _booking.FindAsync(request.BatchId);
        if (batch is null)
            return BadRequest(new { message = "That time block no longer exists." });

        if (batch.BatchDate <= DateTime.Today)
            return BadRequest(new { message = "Choose a date from tomorrow onwards." });

        // The same rules the front desk books under.
        var refusal = BatchBooking.Check(batch, patientId.Value);
        if (refusal is not null)
        {
            return refusal.IsConflict
                ? Conflict(new { message = refusal.Message })
                : BadRequest(new { message = refusal.Message });
        }

        var startsAt = BatchBooking.StartsAt(batch);

        var sameDay = await _db.Appointments
            .CountAsync(a => a.ScheduledAt >= batch.BatchDate && a.ScheduledAt < batch.BatchDate.AddDays(1));

        var appointment = new Appointment
        {
            AppointmentCode = $"APT-{batch.BatchDate:yyyyMMdd}-{sameDay + 1:D4}",
            PatientId = patientId.Value,
            DoctorId = doctor.Id,
            Service = service,
            ScheduledAt = startsAt,
            Type = "Scheduled",
            // Booked without anyone at the desk having seen it, so it waits to
            // be confirmed rather than counting as arranged.
            Status = "Pending",
            Notes = (request.Notes ?? string.Empty).Trim(),
            BatchId = batch.Id,
            QueuePosition = BatchBooking.NextPosition(batch)
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Requested for {batch.BatchDate:dddd, d MMMM} at {BatchBooking.Display(batch.StartTime)}.",
            appointment.AppointmentCode,
            scheduledAt = appointment.ScheduledAt,
            appointment.Status,
            doctor = ("Dr. " + doctor.FirstName + " " + doctor.LastName).Trim(),
            service
        });
    }

    /// <summary>
    /// One of their own result files. The patient id comes from the token and
    /// is part of the lookup, so another patient's document simply is not found.
    /// </summary>
    [HttpGet("me/documents/{documentId:int}")]
    public async Task<IActionResult> GetDocument(int documentId)
    {
        var patientId = CallerId();
        if (patientId is null) return Unauthorized();

        var document = await _db.PatientDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.PatientId == patientId);
        if (document is null) return NotFound();

        Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
        return File(document.Content, document.ContentType);
    }

    private int? CallerId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
