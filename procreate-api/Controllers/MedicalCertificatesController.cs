using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MedicalCertificatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MedicalCertificatesController(AppDbContext db) { _db = db; }

    public static readonly string[] Templates = { "General", "Work", "School" };

    public record CertificateDto(
        int Id, string CertificateNumber, int? PatientId, string IssuedTo, string PatientCode,
        int DoctorId, string DoctorName, DateTime IssueDate, string Template,
        string Diagnosis, string Recommendation, string Remarks, bool IsWalkIn);

    /// <summary>
    /// Every text field is nullable on purpose. Non-nullable strings get an
    /// implicit [Required] under nullable reference types, so omitting an
    /// optional field would fail model binding before any of our own
    /// validation runs. Required-ness is enforced in ValidateAsync instead.
    /// </summary>
    public record CertificateWriteRequest(
        int? PatientId, string? WalkInName, string? WalkInAge, string? WalkInAddress,
        int DoctorId, DateTime IssueDate, string? Template,
        string? Diagnosis, string? Recommendation, string? Remarks);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, [FromQuery] string? template,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.MedicalCertificates
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.CertificateNumber.Contains(term) ||
                c.WalkInName.Contains(term) ||
                (c.Patient != null &&
                    (c.Patient.FirstName.Contains(term) ||
                     c.Patient.LastName.Contains(term) ||
                     c.Patient.PatientCode.Contains(term))));
        }

        if (!string.IsNullOrWhiteSpace(template))
            query = query.Where(c => c.Template == template);

        var total = await query.CountAsync();

        var certificates = await query
            .OrderByDescending(c => c.IssueDate).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => Project(c))
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = certificates });
    }

    [HttpGet("templates")]
    public IActionResult GetTemplates() => Ok(Templates);

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var certificate = await _db.MedicalCertificates
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .Where(c => c.Id == id)
            .Select(c => Project(c))
            .FirstOrDefaultAsync();

        return certificate is null ? NotFound() : Ok(certificate);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CertificateWriteRequest request)
    {
        var validation = await ValidateAsync(request);
        if (validation is not null) return validation;

        var certificate = new MedicalCertificate
        {
            CertificateNumber = await NextNumberAsync(request.IssueDate),
            PatientId = request.PatientId,
            WalkInName = request.WalkInName?.Trim() ?? "",
            WalkInAge = request.WalkInAge?.Trim() ?? "",
            WalkInAddress = request.WalkInAddress?.Trim() ?? "",
            DoctorId = request.DoctorId,
            IssueDate = request.IssueDate == default ? DateTime.Today : request.IssueDate,
            Template = request.Template!,
            Diagnosis = request.Diagnosis!.Trim(),
            Recommendation = request.Recommendation?.Trim() ?? "",
            Remarks = request.Remarks?.Trim() ?? "",
        };

        _db.MedicalCertificates.Add(certificate);
        await _db.SaveChangesAsync();

        var created = await _db.MedicalCertificates
            .Include(c => c.Patient).Include(c => c.Doctor)
            .Where(c => c.Id == certificate.Id)
            .Select(c => Project(c))
            .FirstAsync();

        return CreatedAtAction(nameof(GetById), new { id = certificate.Id }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var certificate = await _db.MedicalCertificates.FindAsync(id);
        if (certificate is null) return NotFound();

        _db.MedicalCertificates.Remove(certificate);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult?> ValidateAsync(CertificateWriteRequest request)
    {
        // Exactly one recipient: a registered patient or a named walk-in.
        var hasPatient = request.PatientId.HasValue;
        var hasWalkIn = !string.IsNullOrWhiteSpace(request.WalkInName);

        if (hasPatient == hasWalkIn)
        {
            return BadRequest(new
            {
                message = "Provide either a registered patient or a walk-in name, not both."
            });
        }

        if (hasPatient && !await _db.Patients.AnyAsync(p => p.Id == request.PatientId))
            return BadRequest(new { message = "Selected patient does not exist." });

        if (!await _db.Doctors.AnyAsync(d => d.Id == request.DoctorId))
            return BadRequest(new { message = "Selected attending physician does not exist." });

        if (string.IsNullOrWhiteSpace(request.Template) || !Templates.Contains(request.Template))
            return BadRequest(new { message = $"Template must be one of: {string.Join(", ", Templates)}." });

        if (string.IsNullOrWhiteSpace(request.Diagnosis))
            return BadRequest(new { message = "Diagnosis / medical findings is required." });

        return null;
    }

    /// <summary>Flat projection — keeps the Patient/Doctor graph out of the JSON.</summary>
    private static CertificateDto Project(MedicalCertificate c) => new(
        c.Id,
        c.CertificateNumber,
        c.PatientId,
        c.Patient != null
            ? (c.Patient.FirstName + " " + c.Patient.LastName).Trim()
            : c.WalkInName,
        c.Patient != null ? c.Patient.PatientCode : "",
        c.DoctorId,
        ("Dr. " + c.Doctor.FirstName + " " + c.Doctor.LastName).Trim(),
        c.IssueDate,
        c.Template,
        c.Diagnosis,
        c.Recommendation,
        c.Remarks,
        c.PatientId == null);

    /// <summary>
    /// MC-yyyyMMdd-XXXX. The suffix is random rather than sequential so the
    /// number does not leak how many certificates a clinic issues.
    /// </summary>
    private async Task<string> NextNumberAsync(DateTime issueDate)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";
        var date = (issueDate == default ? DateTime.Today : issueDate).ToString("yyyyMMdd");

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var suffix = new string(Enumerable.Range(0, 4)
                .Select(_ => alphabet[Random.Shared.Next(alphabet.Length)])
                .ToArray());

            var candidate = $"MC-{date}-{suffix}";

            if (!await _db.MedicalCertificates.AnyAsync(c => c.CertificateNumber == candidate))
                return candidate;
        }

        // Fall back to something guaranteed unique rather than failing the issue.
        return $"MC-{date}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
