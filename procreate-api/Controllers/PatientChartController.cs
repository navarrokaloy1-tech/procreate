using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// The clinical record hanging off a patient: allergies, medications, history,
/// vitals, uploaded result files and their diagnostic order history.
///
/// Separate from <see cref="PatientsController"/>, which owns the demographic
/// record. The list sections are replace-all rather than per-row CRUD: the UI
/// edits each one as a whole list and saves it, so a partial API would only
/// give the client more ways to leave the chart half-written.
/// </summary>
[ApiController]
[Route("api/patients/{patientId:int}")]
public class PatientChartController : ControllerBase
{
    /// <summary>Anything larger is a scan that belongs in a document system.</summary>
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly AppDbContext _db;
    public PatientChartController(AppDbContext db) => _db = db;

    // ----------------------------------------------------------
    // The whole chart in one call
    // ----------------------------------------------------------

    [HttpGet("chart")]
    public async Task<IActionResult> GetChart(int patientId)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient is null) return NotFound(new { message = "Patient not found." });

        var allergies = await _db.PatientAllergies
            .Where(a => a.PatientId == patientId)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Substance, a.Severity, a.Reaction })
            .ToListAsync();

        var medications = await _db.PatientMedications
            .Where(m => m.PatientId == patientId)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Name, m.Dosage, m.Frequency, m.Notes })
            .ToListAsync();

        var conditions = await _db.PatientConditions
            .Where(c => c.PatientId == patientId)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Condition, c.DiagnosedOn, c.Notes })
            .ToListAsync();

        var vitals = await _db.VitalSignRecords
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.RecordedAt)
            .Select(v => new
            {
                v.Id, v.RecordedAt, v.RecordedBy,
                v.SystolicBp, v.DiastolicBp, v.HeartRate, v.RespiratoryRate,
                v.TemperatureC, v.WeightKg, v.HeightCm, v.OxygenSaturation, v.Notes
            })
            .ToListAsync();

        // Content is deliberately not projected here: the list needs metadata only.
        var documents = await _db.PatientDocuments
            .Where(d => d.PatientId == patientId)
            .OrderByDescending(d => d.ResultDate).ThenByDescending(d => d.Id)
            .Select(d => new
            {
                d.Id, d.Department, d.FileName, d.ContentType,
                d.SizeBytes, d.ResultDate, d.UploadedBy, d.UploadedAt
            })
            .ToListAsync();

        var orders = await _db.LabOrders
            .Include(o => o.Visit)
            .Include(o => o.LabTest).ThenInclude(t => t.Category)
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Where(o => o.Visit.PatientId == patientId)
            .OrderByDescending(o => o.Id)
            .ToListAsync();

        var labHistory = orders.Select(o => new
        {
            o.Id,
            orderCode = LabOrderCode(o),
            department = o.LabTest.Category.Department,
            categoryName = o.LabTest.Category.Name,
            testName = o.LabTest.Name,
            o.Status,
            stage = StageFor(o.Status),
            o.IsAbnormal,
            resultKind = o.LabTest.Parameters.Count > 0 ? "parameters" : "narrative",
            resultDate = o.ReleasedAt ?? o.ProcessedAt ?? o.Visit.VisitDate,
            visitCode = o.Visit.VisitCode
        });

        return Ok(new
        {
            patient = new
            {
                patient.Id,
                patient.PatientCode,
                fullName = FullName(patient),
                patient.FirstName,
                patient.MiddleName,
                patient.LastName,
                patient.Suffix,
                patient.DateOfBirth,
                age = LabController.AgeOn(patient.DateOfBirth, DateTime.Today),
                patient.Gender,
                patient.CivilStatus,
                patient.Occupation,
                patient.Nationality,
                patient.BloodType,
                patient.ContactNumber,
                patient.Landline,
                patient.Email,
                address = FullAddress(patient),
                patient.EmergencyContactName,
                patient.EmergencyContactRelationship,
                patient.EmergencyContactNumber,
                patient.PhilHealthNumber,
                patient.SeniorCitizenId,
                patient.PwdId,
                patient.HmoProvider,
                patient.HmoAccountNumber,
                patient.CreatedAt
            },
            allergies,
            medications,
            conditions,
            vitals,
            documents,
            labHistory
        });
    }

    // ----------------------------------------------------------
    // Allergies / medications / history - replace the whole list
    // ----------------------------------------------------------

    [HttpPut("allergies")]
    public async Task<IActionResult> ReplaceAllergies(int patientId, [FromBody] AllergyListRequest request)
    {
        if (!await PatientExists(patientId)) return NotFound(new { message = "Patient not found." });

        var existing = await _db.PatientAllergies.Where(a => a.PatientId == patientId).ToListAsync();
        _db.PatientAllergies.RemoveRange(existing);

        foreach (var item in request.Items ?? new List<AllergyInput>())
        {
            if (string.IsNullOrWhiteSpace(item.Substance)) continue;
            _db.PatientAllergies.Add(new PatientAllergy
            {
                PatientId = patientId,
                Substance = item.Substance.Trim(),
                Severity = item.Severity?.Trim() ?? string.Empty,
                Reaction = item.Reaction?.Trim() ?? string.Empty
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Allergies updated." });
    }

    [HttpPut("medications")]
    public async Task<IActionResult> ReplaceMedications(int patientId, [FromBody] MedicationListRequest request)
    {
        if (!await PatientExists(patientId)) return NotFound(new { message = "Patient not found." });

        var existing = await _db.PatientMedications.Where(m => m.PatientId == patientId).ToListAsync();
        _db.PatientMedications.RemoveRange(existing);

        foreach (var item in request.Items ?? new List<MedicationInput>())
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;
            _db.PatientMedications.Add(new PatientMedication
            {
                PatientId = patientId,
                Name = item.Name.Trim(),
                Dosage = item.Dosage?.Trim() ?? string.Empty,
                Frequency = item.Frequency?.Trim() ?? string.Empty,
                Notes = item.Notes?.Trim() ?? string.Empty
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Medications updated." });
    }

    [HttpPut("conditions")]
    public async Task<IActionResult> ReplaceConditions(int patientId, [FromBody] ConditionListRequest request)
    {
        if (!await PatientExists(patientId)) return NotFound(new { message = "Patient not found." });

        var existing = await _db.PatientConditions.Where(c => c.PatientId == patientId).ToListAsync();
        _db.PatientConditions.RemoveRange(existing);

        foreach (var item in request.Items ?? new List<ConditionInput>())
        {
            if (string.IsNullOrWhiteSpace(item.Condition)) continue;
            _db.PatientConditions.Add(new PatientCondition
            {
                PatientId = patientId,
                Condition = item.Condition.Trim(),
                DiagnosedOn = item.DiagnosedOn?.Trim() ?? string.Empty,
                Notes = item.Notes?.Trim() ?? string.Empty
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Medical history updated." });
    }

    // ----------------------------------------------------------
    // Vital signs - appended, never edited in place
    // ----------------------------------------------------------

    [HttpPost("vitals")]
    public async Task<IActionResult> AddVitals(int patientId, [FromBody] VitalsInput input)
    {
        if (!await PatientExists(patientId)) return NotFound(new { message = "Patient not found." });

        var record = new VitalSignRecord
        {
            PatientId = patientId,
            RecordedAt = input.RecordedAt ?? DateTime.UtcNow,
            RecordedBy = input.RecordedBy?.Trim() ?? string.Empty,
            SystolicBp = input.SystolicBp,
            DiastolicBp = input.DiastolicBp,
            HeartRate = input.HeartRate,
            RespiratoryRate = input.RespiratoryRate,
            TemperatureC = input.TemperatureC,
            WeightKg = input.WeightKg,
            HeightCm = input.HeightCm,
            OxygenSaturation = input.OxygenSaturation,
            Notes = input.Notes?.Trim() ?? string.Empty
        };

        _db.VitalSignRecords.Add(record);
        await _db.SaveChangesAsync();
        return Ok(new { record.Id });
    }

    [HttpDelete("vitals/{vitalId:int}")]
    public async Task<IActionResult> DeleteVitals(int patientId, int vitalId)
    {
        var record = await _db.VitalSignRecords
            .FirstOrDefaultAsync(v => v.Id == vitalId && v.PatientId == patientId);
        if (record is null) return NotFound();

        _db.VitalSignRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----------------------------------------------------------
    // Result files
    // ----------------------------------------------------------

    [HttpPost("documents")]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 64)]
    public async Task<IActionResult> UploadDocument(
        int patientId,
        [FromForm] IFormFile? file,
        [FromForm] string? department,
        [FromForm] DateTime? resultDate,
        [FromForm] string? uploadedBy)
    {
        if (!await PatientExists(patientId)) return NotFound(new { message = "Patient not found." });
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file was uploaded." });
        if (file.Length > MaxUploadBytes)
            return BadRequest(new { message = "The file is larger than the 10 MB limit." });

        var dept = Departments.All.Contains(department) ? department! : Departments.Laboratory;

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        var document = new PatientDocument
        {
            PatientId = patientId,
            Department = dept,
            FileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            SizeBytes = file.Length,
            ResultDate = resultDate ?? DateTime.Today,
            UploadedBy = uploadedBy?.Trim() ?? string.Empty,
            Content = buffer.ToArray()
        };

        _db.PatientDocuments.Add(document);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            document.Id, document.Department, document.FileName,
            document.ContentType, document.SizeBytes, document.ResultDate,
            document.UploadedBy, document.UploadedAt
        });
    }

    /// <summary>
    /// Streams the file back. Served inline so a PDF opens in the built-in
    /// viewer rather than downloading, which is what "view result" should do.
    /// </summary>
    [HttpGet("documents/{documentId:int}")]
    public async Task<IActionResult> GetDocument(int patientId, int documentId)
    {
        var document = await _db.PatientDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.PatientId == patientId);
        if (document is null) return NotFound();

        Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
        return File(document.Content, document.ContentType);
    }

    [HttpDelete("documents/{documentId:int}")]
    public async Task<IActionResult> DeleteDocument(int patientId, int documentId)
    {
        var document = await _db.PatientDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.PatientId == patientId);
        if (document is null) return NotFound();

        _db.PatientDocuments.Remove(document);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private Task<bool> PatientExists(int patientId) =>
        _db.Patients.AnyAsync(p => p.Id == patientId);

    private static string FullName(Patient p) =>
        string.Join(" ", new[] { p.FirstName, p.MiddleName, p.LastName, p.Suffix }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Street line plus whichever parts of the structured address are set.</summary>
    private static string FullAddress(Patient p) =>
        string.Join(", ", new[] { p.Address, p.City, p.Province, p.Region, p.ZipCode }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string StageFor(string status) => status switch
    {
        "Ordered" => "Pending",
        "Collected" => "In Progress",
        "Resulted" => "For Reading",
        "Released" => "Completed",
        _ => status
    };

    private static string LabOrderCode(LabOrder o)
    {
        var prefix = o.LabTest.Category.Department switch
        {
            Departments.Imaging => "XR",
            Departments.Ultrasound => "US",
            Departments.HeartStation => "HS",
            _ => "LAB"
        };
        return $"{prefix}-{o.Visit.VisitDate:yyyyMMdd}-{o.Id:D4}";
    }
}

public record AllergyInput(string Substance, string? Severity, string? Reaction);
public record AllergyListRequest(List<AllergyInput>? Items);

public record MedicationInput(string Name, string? Dosage, string? Frequency, string? Notes);
public record MedicationListRequest(List<MedicationInput>? Items);

public record ConditionInput(string Condition, string? DiagnosedOn, string? Notes);
public record ConditionListRequest(List<ConditionInput>? Items);

public record VitalsInput(
    DateTime? RecordedAt,
    string? RecordedBy,
    int? SystolicBp,
    int? DiastolicBp,
    int? HeartRate,
    int? RespiratoryRate,
    decimal? TemperatureC,
    decimal? WeightKg,
    decimal? HeightCm,
    int? OxygenSaturation,
    string? Notes);
