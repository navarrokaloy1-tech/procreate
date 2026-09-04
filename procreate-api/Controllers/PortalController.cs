using ProCreateApi.Data;
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
    public PortalController(AppDbContext db) => _db = db;

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
