using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Lis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILisService _lis;

    public PatientsController(AppDbContext db, ILisService lis) { _db = db; _lis = lis; }

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
