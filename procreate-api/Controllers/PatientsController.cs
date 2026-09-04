using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Lis;
using ProCreateApi.Services.Queue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILisService _lis;
    private readonly QueueAllocator _queue;

    public PatientsController(AppDbContext db, ILisService lis, QueueAllocator queue)
    {
        _db = db;
        _lis = lis;
        _queue = queue;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? gender, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Patients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(gender))
        {
            query = query.Where(p => p.Gender == gender);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Tokenized search: each word must match some field, so multi-word /
            // "LastName, FirstName" queries (as shown in the UI dropdown) still match.
            var tokens = search.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var term = token;
                query = query.Where(p =>
                    p.FirstName.Contains(term) || p.LastName.Contains(term) ||
                    p.MiddleName.Contains(term) || p.PatientCode.Contains(term) ||
                    p.ContactNumber.Contains(term) || p.Email.Contains(term));
            }
        }

        var total = await query.CountAsync();
        var patients = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new { total, page, pageSize, data = patients });
    }

    /// <summary>
    /// Exact lookup by patient code, for QR scanning. Accepts the bare code or
    /// a "patient:CODE" payload so a scanned card can carry a scheme.
    /// </summary>
    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "No patient code was supplied." });

        var trimmed = code.Trim();
        const string scheme = "patient:";
        if (trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[scheme.Length..].Trim();

        var patient = await _db.Patients
            .FirstOrDefaultAsync(p => p.PatientCode == trimmed);

        return patient is null
            ? NotFound(new { message = $"No patient found for code \"{trimmed}\"." })
            : Ok(patient);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var patient = await _db.Patients
            .Include(p => p.Visits).ThenInclude(v => v.LabOrders).ThenInclude(o => o.LabTest)
            .FirstOrDefaultAsync(p => p.Id == id);
        return patient is null ? NotFound() : Ok(patient);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Patient patient)
    {
        var count = await _db.Patients.CountAsync();
        patient.PatientCode = $"PT-{DateTime.Now:yyyyMMdd}-{(count + 1):D4}";
        patient.CreatedAt = DateTime.UtcNow;
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        _ = _lis.SendPatientAsync(patient, isUpdate: false);
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
    }

    /// <summary>
    /// Kiosk registration: creates the patient and puts them in today's
    /// reception queue in one call.
    ///
    /// Deliberately separate from POST /patients. Staff registering someone
    /// should not silently queue them, and doing this as two client calls
    /// could leave a patient registered but not queued — which is the exact
    /// state the kiosk used to leave them in while telling them otherwise.
    /// </summary>
    [HttpPost("self-register")]
    public async Task<IActionResult> SelfRegister(SelfRegisterRequest request)
    {
        var patient = request.Patient ?? new Patient();

        if (!string.IsNullOrEmpty(request.Password))
        {
            if (request.Password.Length < 8)
                return BadRequest(new { message = "Choose a password of at least 8 characters." });

            if (string.IsNullOrWhiteSpace(patient.Email) && string.IsNullOrWhiteSpace(patient.ContactNumber))
                return BadRequest(new { message = "An email address or mobile number is needed to sign in with." });

            patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            patient.PortalEnabled = true;
        }

        var count = await _db.Patients.CountAsync();
        patient.PatientCode = $"PT-{DateTime.Now:yyyyMMdd}-{(count + 1):D4}";
        patient.CreatedAt = DateTime.UtcNow;
        // Minted for every patient, so a card can be issued later without
        // re-registering them.
        patient.CardToken = AuthController.NewCardToken();

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        _ = _lis.SendPatientAsync(patient, isUpdate: false);

        var fullName = $"{patient.FirstName} {patient.LastName}".Trim();
        var entry = await _queue.EnqueueAsync(fullName, patient.Id);

        // The patient exists either way. Report the queue separately so the
        // kiosk can show the card and point them at reception rather than
        // claiming registration failed.
        return Ok(new
        {
            patient.Id,
            patient.PatientCode,
            patient.FirstName,
            patient.MiddleName,
            patient.LastName,
            // Goes into the card QR. It is the sign-in secret, so it is
            // returned once here and never listed by any other endpoint.
            patient.CardToken,
            patient.PortalEnabled,
            queued = entry is not null,
            queueNumber = entry?.QueueNumber,
            queueEntryId = entry?.Id
        });
    }

    /// <summary>
    /// Issues a replacement card, rotating the token so a lost card stops
    /// working. Returns the new secret once, for printing.
    /// </summary>
    [HttpPost("{id}/card")]
    public async Task<IActionResult> ReissueCard(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient is null) return NotFound();

        patient.CardToken = AuthController.NewCardToken();
        await _db.SaveChangesAsync();

        return Ok(new { patient.Id, patient.PatientCode, patient.CardToken });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Patient updated)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient is null) return NotFound();

        patient.FirstName = updated.FirstName;
        patient.LastName = updated.LastName;
        patient.MiddleName = updated.MiddleName;
        patient.DateOfBirth = updated.DateOfBirth;
        patient.Gender = updated.Gender;
        patient.ContactNumber = updated.ContactNumber;
        patient.Email = updated.Email;
        patient.Address = updated.Address;
        patient.BloodType = updated.BloodType;
        patient.EmergencyContactName = updated.EmergencyContactName;
        patient.EmergencyContactNumber = updated.EmergencyContactNumber;
        patient.CivilStatus = updated.CivilStatus;
        patient.Nationality = updated.Nationality;
        patient.Occupation = updated.Occupation;
        patient.Suffix = updated.Suffix;
        patient.Landline = updated.Landline;
        patient.Country = updated.Country;
        patient.Region = updated.Region;
        patient.Province = updated.Province;
        patient.City = updated.City;
        patient.ZipCode = updated.ZipCode;
        patient.EmergencyContactRelationship = updated.EmergencyContactRelationship;
        patient.EmergencyContactNotes = updated.EmergencyContactNotes;
        patient.PhotoUrl = updated.PhotoUrl;
        patient.PhilHealthNumber = updated.PhilHealthNumber;
        patient.SeniorCitizenId = updated.SeniorCitizenId;
        patient.PwdId = updated.PwdId;
        patient.HmoProvider = updated.HmoProvider;
        patient.HmoAccountNumber = updated.HmoAccountNumber;
        await _db.SaveChangesAsync();
        _ = _lis.SendPatientAsync(patient, isUpdate: true);
        return Ok(patient);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient is null) return NotFound();
        _db.Patients.Remove(patient);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var today = DateTime.UtcNow.Date;
        return Ok(new
        {
            totalPatients = await _db.Patients.CountAsync(),
            todayNew = await _db.Patients.CountAsync(p => p.CreatedAt.Date == today),
            totalVisits = await _db.Visits.CountAsync(),
            todayVisits = await _db.Visits.CountAsync(v => v.VisitDate.Date == today),
            pendingResults = await _db.LabOrders.CountAsync(o => o.Status == "Ordered" || o.Status == "Processing"),
            totalRevenue = await _db.Bills.Where(b => b.Status == "Paid").SumAsync(b => b.Total),
            todayRevenue = await _db.Bills.Where(b => b.Status == "Paid" && b.CreatedAt.Date == today).SumAsync(b => b.Total)
        });
    }
}

/// <summary>
/// Kiosk registration. Password is optional: a walk-in can be registered
/// without a portal account, and set one up later.
/// </summary>
public record SelfRegisterRequest(Patient? Patient, string? Password);
