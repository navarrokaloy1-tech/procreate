using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LabController : ControllerBase
{
    private readonly AppDbContext _db;
    public LabController(AppDbContext db) => _db = db;

    [HttpGet("tests")]
    public async Task<IActionResult> GetTests()
    {
        var tests = await _db.LabTests.Include(t => t.Category).Include(t => t.Parameters).ToListAsync();
        return Ok(tests);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] string? department)
    {
        var query = _db.TestCategories.Include(c => c.Tests).AsQueryable();
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(c => c.Department == department);

        var cats = await query.OrderBy(c => c.Id).ToListAsync();
        return Ok(cats);
    }

    /// <summary>The department tabs, with a live order count for each.</summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var counts = await _db.LabOrders
            .GroupBy(o => o.LabTest.Category.Department)
            .Select(g => new { department = g.Key, count = g.Count() })
            .ToListAsync();

        var data = Departments.All.Select(d => new
        {
            name = d,
            count = counts.FirstOrDefault(c => c.department == d)?.count ?? 0
        });
        return Ok(data);
    }

    /// <summary>
    /// Orders for one department tab. <paramref name="status"/> takes the stored
    /// status ("Ordered", "Collected", ...) or the stage label the console shows
    /// ("Pending", "In Progress", ...) — see <see cref="StatusesForStage"/>.
    /// </summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status,
        [FromQuery] string? department,
        [FromQuery] string? search)
    {
        var query = _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest).ThenInclude(t => t.Category)
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(o => o.Results)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(o => o.LabTest.Category.Department == department);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = StatusesForStage(status);
            query = query.Where(o => wanted.Contains(o.Status));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.SpecimenBarcode.Contains(term) ||
                o.LabTest.Name.Contains(term) ||
                o.LabTest.Code.Contains(term) ||
                o.Visit.VisitCode.Contains(term) ||
                o.Visit.Patient.PatientCode.Contains(term) ||
                o.Visit.Patient.FirstName.Contains(term) ||
                o.Visit.Patient.LastName.Contains(term));
        }

        var orders = await query.OrderByDescending(o => o.Id).ToListAsync();
        var data = orders.Select(o => new
        {
            o.Id,
            orderCode = OrderCode(o),
            visitCode = o.Visit.VisitCode,
            patientId = o.Visit.PatientId,
            patientName = $"{o.Visit.Patient.FirstName} {o.Visit.Patient.LastName}".Trim(),
            patientCode = o.Visit.Patient.PatientCode,
            testName = o.LabTest.Name,
            testCode = o.LabTest.Code,
            department = o.LabTest.Category.Department,
            categoryName = o.LabTest.Category.Name,
            o.SpecimenBarcode,
            o.Status,
            stage = StageFor(o.Status),
            o.IsAbnormal,
            resultKind = o.LabTest.Parameters.Count > 0 ? "parameters" : "narrative",
            orderedDate = o.Visit.VisitDate,
            collectedDate = o.CollectedAt,
            releasedDate = o.ReleasedAt
        });
        return Ok(data);
    }

    // The console groups the four stored statuses into the stage labels staff
    // use. Both live here so the list filter and the row badge cannot drift.
    private static string StageFor(string status) => status switch
    {
        "Ordered" => "Pending",
        "Collected" => "In Progress",
        "Resulted" => "For Reading",
        "Released" => "Completed",
        _ => status
    };

    private static string[] StatusesForStage(string stage) => stage switch
    {
        "Pending" => new[] { "Ordered" },
        "In Progress" => new[] { "Collected" },
        "For Reading" => new[] { "Resulted" },
        "Completed" => new[] { "Released" },
        _ => new[] { stage }
    };

    /// <summary>
    /// Human-facing order number: department prefix, order date, sequence.
    /// Built from stored fields so it is stable without another column.
    /// </summary>
    private static string OrderCode(LabOrder o)
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

    [HttpGet("orders/{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest).ThenInclude(t => t.Category)
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(o => o.Results).ThenInclude(r => r.TestParameter)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        static decimal? ParseNullable(string? s) => decimal.TryParse(s, out var d) ? d : (decimal?)null;

        var patient = order.Visit.Patient;
        var result = new
        {
            order.Id,
            orderCode = OrderCode(order),
            visitCode = order.Visit.VisitCode,
            testName = order.LabTest.Name,
            testCode = order.LabTest.Code,
            department = order.LabTest.Category.Department,
            categoryName = order.LabTest.Category.Name,
            specimen = order.LabTest.Specimen,
            method = order.LabTest.Method,
            order.Status,
            stage = StageFor(order.Status),
            order.SpecimenBarcode,
            order.NarrativeFindings,
            order.IsAbnormal,
            order.ResultedBy,
            requestedBy = order.Visit.ReferringPhysician,

            // The reading pages need the patient banner without a second call.
            patient = new
            {
                id = patient.Id,
                patientCode = patient.PatientCode,
                name = $"{patient.FirstName} {patient.LastName}".Trim(),
                patient.Gender,
                patient.DateOfBirth,
                age = AgeOn(patient.DateOfBirth, DateTime.Today)
            },
            patientName = $"{patient.FirstName} {patient.LastName}".Trim(),

            orderedDate = order.Visit.VisitDate,
            order.CollectedAt,
            order.ProcessedAt,
            order.ReleasedAt,

            // A study with no parameters is written up in prose instead.
            resultKind = order.LabTest.Parameters.Count > 0 ? "parameters" : "narrative",
            parameters = order.LabTest.Parameters.Select(p =>
            {
                var existing = order.Results.FirstOrDefault(r => r.TestParameterId == p.Id);
                return new
                {
                    id = p.Id,
                    parameterName = p.Name,
                    unit = p.Unit,
                    referenceRangeMin = ParseNullable(p.NormalMin),
                    referenceRangeMax = ParseNullable(p.NormalMax),
                    referenceRangeText = p.ReferenceRange,
                    value = existing?.Value ?? "",
                    flag = existing?.Flag ?? ""
                };
            })
        };
        return Ok(result);
    }

    /// <summary>Whole years elapsed, counting the birthday that has not come round yet.</summary>
    internal static int AgeOn(DateTime dateOfBirth, DateTime asOf)
    {
        var age = asOf.Year - dateOfBirth.Year;
        if (asOf < dateOfBirth.AddYears(age)) age--;
        return age < 0 ? 0 : age;
    }

    [HttpPost("orders/{id}/collect")]
    public async Task<IActionResult> CollectSpecimen(int id)
    {
        var order = await _db.LabOrders.FindAsync(id);
        if (order is null) return NotFound();
        order.Status = "Collected";
        order.CollectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpPost("orders/{id}/results")]
    public async Task<IActionResult> EncodeResults(int id, [FromBody] EncodeResultsRequest request)
    {
        var order = await _db.LabOrders
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(o => o.Results)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        _db.LabResults.RemoveRange(order.Results);
        foreach (var entry in request.Results ?? new List<ResultEntry>())
        {
            var param = order.LabTest.Parameters.FirstOrDefault(p => p.Id == entry.ParameterId);
            string flag = "Normal";
            if (param != null && double.TryParse(entry.Value, out var val))
            {
                if (double.TryParse(param.NormalMin, out var min) && val < min) flag = "Low";
                else if (double.TryParse(param.NormalMax, out var max) && val > max) flag = "High";
            }
            _db.LabResults.Add(new LabResult
            {
                LabOrderId = id,
                TestParameterId = entry.ParameterId,
                Value = entry.Value,
                Flag = flag,
                Remarks = entry.Remarks
            });
        }
        // Narrative studies carry no parameters, so the write-up and the
        // reader's abnormal flag are what actually gets saved for them.
        order.NarrativeFindings = request.NarrativeFindings ?? string.Empty;
        order.IsAbnormal = request.IsAbnormal;
        order.ResultedBy = request.ResultedBy ?? string.Empty;
        order.Status = "Resulted";
        order.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Results saved" });
    }

    [HttpPost("orders/{id}/release")]
    public async Task<IActionResult> ReleaseResult(int id)
    {
        var order = await _db.LabOrders.FindAsync(id);
        if (order is null) return NotFound();
        order.Status = "Released";
        order.ReleasedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpGet("orders/{id}/qr")]
    public async Task<IActionResult> GetQrCode(int id)
    {
        var order = await _db.LabOrders.Include(o => o.Visit).ThenInclude(v => v.Patient).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        using var qrGenerator = new QRCoder.QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode($"PRO-CREATE|ORDER:{id}|PATIENT:{order.Visit.PatientId}|BARCODE:{order.SpecimenBarcode}", QRCoder.QRCodeGenerator.ECCLevel.Q);
        var qrCode = new QRCoder.PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(10);
        return File(qrBytes, "image/png");
    }
}

public record ResultEntry(int ParameterId, string Value, string? Remarks);

public record EncodeResultsRequest(
    List<ResultEntry>? Results,
    string? Remarks,
    string? NarrativeFindings,
    bool IsAbnormal,
    string? ResultedBy);
