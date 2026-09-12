using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DoctorsController(AppDbContext db) { _db = db; }

    public record DoctorListDto(
        int Id, string DoctorCode, string FullName, string Specialty, string SubSpecialty,
        string Email, string ContactNumber, string PrcLicenseNumber,
        decimal ConsultationFee, bool IsActive, bool HasLogin);

    public record ScheduleDto(int DayOfWeek, bool IsAvailable, string StartTime, string EndTime);

    public record DoctorDetailDto(
        int Id, string DoctorCode, string FirstName, string MiddleName, string LastName,
        string Suffix, string Gender, string Specialty, string SubSpecialty, string Email,
        string ContactNumber, string PrcLicenseNumber, DateTime? PrcLicenseExpiry,
        string PtrNumber, string S2LicenseNumber, string TinNumber,
        decimal ConsultationFee, decimal FollowUpFee, decimal SpecialistFee,
        string Bio, bool IsActive, int? UserId, bool HasSignature, List<ScheduleDto> Schedules);

    public record DoctorWriteRequest(
        string FirstName, string MiddleName, string LastName, string Suffix, string Gender,
        string Specialty, string SubSpecialty, string Email, string ContactNumber,
        string PrcLicenseNumber, DateTime? PrcLicenseExpiry, string PtrNumber,
        string S2LicenseNumber, string TinNumber, decimal ConsultationFee,
        decimal FollowUpFee, decimal SpecialistFee, string Bio, bool IsActive, int? UserId);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, [FromQuery] string? specialty, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 15)
    {
        var query = _db.Doctors.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d =>
                d.FirstName.Contains(term) || d.LastName.Contains(term) ||
                d.DoctorCode.Contains(term) || d.PrcLicenseNumber.Contains(term) ||
                d.Email.Contains(term) || d.ContactNumber.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(d => d.Specialty == specialty);

        if (isActive.HasValue)
            query = query.Where(d => d.IsActive == isActive.Value);

        var total = await query.CountAsync();

        var doctors = await query
            .OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new DoctorListDto(
                d.Id, d.DoctorCode,
                ("Dr. " + d.FirstName + " " + d.LastName).Trim(),
                d.Specialty, d.SubSpecialty, d.Email, d.ContactNumber,
                d.PrcLicenseNumber, d.ConsultationFee, d.IsActive, d.UserId != null))
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = doctors });
    }

    /// <summary>Distinct specialties present in the data, for the filter dropdown.</summary>
    [HttpGet("specialties")]
    public async Task<IActionResult> GetSpecialties()
    {
        var specialties = await _db.Doctors
            .Where(d => d.Specialty != "")
            .Select(d => d.Specialty)
            .Distinct().OrderBy(s => s)
            .ToListAsync();

        return Ok(specialties);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var doctor = await _db.Doctors
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor is null) return NotFound();

        return Ok(ToDetail(doctor));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DoctorWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest(new { message = "First and last name are required." });

        if (string.IsNullOrWhiteSpace(request.Specialty))
            return BadRequest(new { message = "Specialty is required." });

        var doctor = new Doctor { DoctorCode = await NextDoctorCodeAsync() };
        Apply(doctor, request);

        // Every doctor gets a full week of schedule rows so the Schedule tab
        // always has something to edit.
        doctor.Schedules = Enumerable.Range(0, 7)
            .Select(day => new DoctorSchedule
            {
                DayOfWeek = day,
                IsAvailable = day >= 1 && day <= 5,
                StartTime = "08:00",
                EndTime = "17:00"
            }).ToList();

        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = doctor.Id }, ToDetail(doctor));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] DoctorWriteRequest request)
    {
        var doctor = await _db.Doctors
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor is null) return NotFound();

        Apply(doctor, request);
        await _db.SaveChangesAsync();

        return Ok(ToDetail(doctor));
    }

    /// <summary>Replace a doctor's weekly availability in one call.</summary>
    [HttpPut("{id}/schedule")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] List<ScheduleDto> schedule)
    {
        var doctor = await _db.Doctors
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor is null) return NotFound();

        foreach (var incoming in schedule)
        {
            if (incoming.DayOfWeek is < 0 or > 6)
                return BadRequest(new { message = "DayOfWeek must be 0 (Sunday) through 6 (Saturday)." });

            var row = doctor.Schedules.FirstOrDefault(s => s.DayOfWeek == incoming.DayOfWeek);
            if (row is null)
            {
                doctor.Schedules.Add(new DoctorSchedule
                {
                    DayOfWeek = incoming.DayOfWeek,
                    IsAvailable = incoming.IsAvailable,
                    StartTime = incoming.StartTime,
                    EndTime = incoming.EndTime
                });
            }
            else
            {
                row.IsAvailable = incoming.IsAvailable;
                row.StartTime = incoming.StartTime;
                row.EndTime = incoming.EndTime;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(ToDetail(doctor));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var doctor = await _db.Doctors.FindAsync(id);
        if (doctor is null) return NotFound();

        // Appointments reference doctors with Restrict, so deactivate rather than
        // orphan a booking history.
        var hasAppointments = await _db.Appointments.AnyAsync(a => a.DoctorId == id);
        if (hasAppointments)
        {
            doctor.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Doctor has appointment history and was deactivated instead of deleted." });
        }

        _db.Doctors.Remove(doctor);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Doctor doctor, DoctorWriteRequest r)
    {
        doctor.FirstName = r.FirstName?.Trim() ?? "";
        doctor.MiddleName = r.MiddleName?.Trim() ?? "";
        doctor.LastName = r.LastName?.Trim() ?? "";
        doctor.Suffix = r.Suffix?.Trim() ?? "";
        doctor.Gender = r.Gender ?? "";
        doctor.Specialty = r.Specialty?.Trim() ?? "";
        doctor.SubSpecialty = r.SubSpecialty?.Trim() ?? "";
        doctor.Email = r.Email?.Trim() ?? "";
        doctor.ContactNumber = r.ContactNumber?.Trim() ?? "";
        doctor.PrcLicenseNumber = r.PrcLicenseNumber?.Trim() ?? "";
        doctor.PrcLicenseExpiry = r.PrcLicenseExpiry;
        doctor.PtrNumber = r.PtrNumber?.Trim() ?? "";
        doctor.S2LicenseNumber = r.S2LicenseNumber?.Trim() ?? "";
        doctor.TinNumber = r.TinNumber?.Trim() ?? "";
        doctor.ConsultationFee = r.ConsultationFee;
        doctor.FollowUpFee = r.FollowUpFee;
        doctor.SpecialistFee = r.SpecialistFee;
        doctor.Bio = r.Bio?.Trim() ?? "";
        doctor.IsActive = r.IsActive;
        doctor.UserId = r.UserId;
    }

    private static DoctorDetailDto ToDetail(Doctor d) => new(
        d.Id, d.DoctorCode, d.FirstName, d.MiddleName, d.LastName, d.Suffix, d.Gender,
        d.Specialty, d.SubSpecialty, d.Email, d.ContactNumber, d.PrcLicenseNumber,
        d.PrcLicenseExpiry, d.PtrNumber, d.S2LicenseNumber, d.TinNumber,
        d.ConsultationFee, d.FollowUpFee, d.SpecialistFee, d.Bio, d.IsActive, d.UserId,
        d.SignatureImage != null && d.SignatureImage.Length > 0,
        d.Schedules.OrderBy(s => s.DayOfWeek)
            .Select(s => new ScheduleDto(s.DayOfWeek, s.IsAvailable, s.StartTime, s.EndTime))
            .ToList());

    // ----------------------------------------------------------
    // Signature
    // ----------------------------------------------------------

    /// <summary>Two megabytes is generous for a signature and stops a scan of
    /// a whole page being stored in a row.</summary>
    private const int MaxSignatureBytes = 2 * 1024 * 1024;

    private static readonly string[] SignatureTypes =
    {
        "image/png", "image/jpeg", "image/jpg", "image/webp", "image/gif"
    };

    /// <summary>
    /// Stores the doctor's signature. It is stamped on results and
    /// certificates issued in their name, so only an image is accepted — a PDF
    /// or a document would not render where those are drawn.
    /// </summary>
    [HttpPost("{id}/signature")]
    [RequestSizeLimit(MaxSignatureBytes + 1024 * 64)]
    public async Task<IActionResult> UploadSignature(int id, [FromForm] IFormFile? file)
    {
        var doctor = await _db.Doctors.FindAsync(id);
        if (doctor is null) return NotFound(new { message = "Doctor not found." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose a signature image to upload." });

        if (file.Length > MaxSignatureBytes)
            return BadRequest(new { message = "The signature must be under 2 MB." });

        var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
        if (!SignatureTypes.Contains(contentType))
            return BadRequest(new { message = "The signature must be a PNG, JPEG, WebP or GIF image." });

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        doctor.SignatureImage = buffer.ToArray();
        doctor.SignatureContentType = contentType;
        doctor.SignatureFileName = Path.GetFileName(file.FileName);
        doctor.SignatureUploadedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Signature saved.",
            doctor.SignatureFileName,
            sizeBytes = doctor.SignatureImage.Length,
            uploadedAt = doctor.SignatureUploadedAt
        });
    }

    /// <summary>
    /// The stored image. Served rather than inlined so the forms and print
    /// sheets can simply point an img at it.
    /// </summary>
    [HttpGet("{id}/signature")]
    public async Task<IActionResult> GetSignature(int id)
    {
        var doctor = await _db.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor?.SignatureImage is null || doctor.SignatureImage.Length == 0)
            return NotFound();

        return File(doctor.SignatureImage, doctor.SignatureContentType);
    }

    [HttpDelete("{id}/signature")]
    public async Task<IActionResult> DeleteSignature(int id)
    {
        var doctor = await _db.Doctors.FindAsync(id);
        if (doctor is null) return NotFound(new { message = "Doctor not found." });

        doctor.SignatureImage = null;
        doctor.SignatureContentType = string.Empty;
        doctor.SignatureFileName = string.Empty;
        doctor.SignatureUploadedAt = null;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Signature removed." });
    }

    private async Task<string> NextDoctorCodeAsync()
    {
        var count = await _db.Doctors.CountAsync();
        return $"DR-{count + 1:D4}";
    }
}
