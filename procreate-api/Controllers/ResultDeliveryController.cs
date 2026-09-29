using System.Text;
using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Clinic;
using ProCreateApi.Services.Email;
using ProCreateApi.Services.Pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace ProCreateApi.Controllers;

/// <summary>
/// Getting a patient's results to them — emailed, or printed to hand over.
///
/// Only released results can be sent. An unreleased result has not been read
/// by a doctor yet, and putting one in front of a patient is the hazard the
/// release step exists to prevent.
/// </summary>
[ApiController]
[Route("api/patients/{patientId:int}/result-delivery")]
public class ResultDeliveryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ClinicSettings _clinic;
    private readonly IWebHostEnvironment _env;
    private readonly ResultSheetPdf _pdf;

    public ResultDeliveryController(
        AppDbContext db, IEmailSender email, ClinicSettings clinic,
        IWebHostEnvironment env, ResultSheetPdf pdf)
    {
        _db = db;
        _email = email;
        _clinic = clinic;
        _env = env;
        _pdf = pdf;
    }

    public record SendRequest(
        List<int> LabOrderIds, List<int> DocumentIds, int DoctorId,
        bool IncludeSignature, string? ToAddress, string? Message);

    public record PackageRequest(
        List<int> LabOrderIds, List<int> DocumentIds, int DoctorId, bool IncludeSignature);

    // ----------------------------------------------------------
    // What can be attached
    // ----------------------------------------------------------

    /// <summary>Released lab results and uploaded files, for the picker.</summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(int patientId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient is null) return NotFound(new { message = "Patient not found." });

        var orders = await ReleasedOrdersQuery(patientId)
            .OrderByDescending(o => o.ReleasedAt)
            .Select(o => new
            {
                o.Id,
                label = o.LabTest.Name,
                department = o.LabTest.Category.Department,
                releasedAt = o.ReleasedAt,
                o.IsAbnormal,
                resultKind = o.LabTest.Parameters.Count > 0 ? "parameters" : "narrative"
            })
            .ToListAsync();

        var documents = await _db.PatientDocuments
            .Where(d => d.PatientId == patientId)
            .OrderByDescending(d => d.ResultDate)
            .Select(d => new
            {
                d.Id,
                label = d.FileName,
                d.Department,
                d.SizeBytes,
                resultDate = d.ResultDate
            })
            .ToListAsync();

        return Ok(new
        {
            patient = new
            {
                patient.Id,
                patient.PatientCode,
                name = $"{patient.FirstName} {patient.LastName}".Trim(),
                patient.Email
            },
            labResults = orders,
            documents
        });
    }

    // ----------------------------------------------------------
    // Assembling
    // ----------------------------------------------------------

    /// <summary>
    /// The assembled sheet, for the preview and for printing. The same package
    /// is what gets rendered into an email body, so what is previewed is what
    /// is sent.
    /// </summary>
    [HttpPost("package")]
    public async Task<IActionResult> BuildPackage(int patientId, [FromBody] PackageRequest request)
    {
        var package = await AssembleAsync(patientId, request.LabOrderIds, request.DoctorId);
        if (package.Error is not null) return package.Error;

        return Ok(new
        {
            clinic = ClinicPayload(),
            package.Patient,
            package.Doctor,
            includeSignature = request.IncludeSignature,
            results = package.Results,
            documents = await DocumentSummariesAsync(patientId, request.DocumentIds),
            generatedAt = DateTime.Now
        });
    }

    // ----------------------------------------------------------
    // Delivering
    // ----------------------------------------------------------

    [HttpPost("send")]
    public async Task<IActionResult> Send(int patientId, [FromBody] SendRequest request)
    {
        var package = await AssembleAsync(patientId, request.LabOrderIds, request.DoctorId);
        if (package.Error is not null) return package.Error;

        // The address on the record wins over anything in the request. Results
        // are personal medical information, so where they go is a property of
        // the patient rather than of whoever pressed Send; a supplied address
        // would otherwise let one mistyped character disclose them. A patient
        // with nothing on file still needs somewhere to send to, so that case
        // falls back to what was given.
        var onFile = (package.PatientEntity!.Email ?? string.Empty).Trim();
        var address = onFile.Length > 0 ? onFile : (request.ToAddress ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(address))
            return BadRequest(new { message = "This patient has no email address on file. Enter one to send to." });

        if (!_email.IsConfigured)
        {
            return StatusCode(503, new
            {
                message = "Email is not set up yet. Add the clinic mail account under "
                        + "\"Email\" in appsettings.json, or use Print instead."
            });
        }

        var attachments = await LoadAttachmentsAsync(patientId, request.DocumentIds);
        var uploadedFileNames = attachments.Select(a => a.FileName).ToList();

        // The doctor's scanned signature, for the sheet.
        byte[]? signatureImage = null;
        if (request.IncludeSignature && package.Doctor!.HasSignature)
        {
            signatureImage = (await _db.Doctors
                .AsNoTracking()
                .Where(d => d.Id == request.DoctorId)
                .Select(d => d.SignatureImage)
                .FirstOrDefaultAsync());
        }

        // The result sheet itself travels as a PDF, so the emailed copy is the
        // same document as the printed one — watermark, letterhead, QR and all,
        // none of which an email body can carry.
        var medTech = package.Results.Select(r => r.ResultedBy).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
        var qrPng = GetPatientQrPng(package.Patient!.PatientCode);
        var sheetPdf = _pdf.Build(
            _clinic, package.Patient!, package.Doctor!, package.Results,
            medTech, request.IncludeSignature, qrPng, signatureImage);

        attachments.Add(new EmailAttachment(
            $"Pro-Create Results - {package.Patient!.PatientCode}.pdf", "application/pdf", sheetPdf));

        var body = RenderCoverNote(package.Patient!, request.Message, uploadedFileNames);

        var subject = $"Your results from Pro-Create — {DateTime.Now:d MMMM yyyy}";
        var outcome = await _email.SendAsync(address, subject, body, attachments);

        // Recorded either way: a failed attempt is part of the trail too.
        _db.ResultDeliveries.Add(new ResultDelivery
        {
            PatientId = patientId,
            DoctorId = request.DoctorId,
            Channel = "Email",
            SentTo = address,
            LabOrderIds = string.Join(",", request.LabOrderIds),
            DocumentIds = string.Join(",", request.DocumentIds ?? new List<int>()),
            IncludeSignature = request.IncludeSignature,
            Status = outcome.Sent ? "Sent" : "Failed",
            FailureReason = outcome.FailureReason,
            SentBy = User.Identity?.Name ?? string.Empty
        });

        await _db.SaveChangesAsync();

        if (!outcome.Sent)
            return StatusCode(502, new { message = $"The email could not be sent: {outcome.FailureReason}" });

        return Ok(new
        {
            message = $"Results sent to {address}.",
            sentTo = address,
            // The result sheet PDF plus any uploaded documents.
            attachments = uploadedFileNames.Count + 1
        });
    }

    /// <summary>Records that a printout was produced, so the trail matches.</summary>
    [HttpPost("print")]
    public async Task<IActionResult> RecordPrint(int patientId, [FromBody] PackageRequest request)
    {
        var package = await AssembleAsync(patientId, request.LabOrderIds, request.DoctorId);
        if (package.Error is not null) return package.Error;

        _db.ResultDeliveries.Add(new ResultDelivery
        {
            PatientId = patientId,
            DoctorId = request.DoctorId,
            Channel = "Print",
            LabOrderIds = string.Join(",", request.LabOrderIds),
            DocumentIds = string.Join(",", request.DocumentIds ?? new List<int>()),
            IncludeSignature = request.IncludeSignature,
            Status = "Printed",
            SentBy = User.Identity?.Name ?? string.Empty
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Printout recorded." });
    }

    /// <summary>What has already gone out for this patient.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(int patientId)
    {
        var history = await _db.ResultDeliveries
            .Include(d => d.Doctor)
            .Where(d => d.PatientId == patientId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(25)
            .Select(d => new
            {
                d.Id,
                d.Channel,
                d.SentTo,
                d.Status,
                d.FailureReason,
                doctorName = ("Dr. " + d.Doctor.FirstName + " " + d.Doctor.LastName).Trim(),
                d.SentBy,
                d.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total = history.Count, data = history });
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private IQueryable<LabOrder> ReleasedOrdersQuery(int patientId) =>
        _db.LabOrders
            .Include(o => o.Visit)
            .Include(o => o.LabTest).ThenInclude(t => t.Category)
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(o => o.Results).ThenInclude(r => r.TestParameter)
            .Where(o => o.Visit.PatientId == patientId && o.Status == "Released");

    // Real types rather than anonymous ones: the email body is assembled by
    // hand from these, and `dynamic` let a wrong member name reach run time.
    public record DeliveryPatientDto(
        int Id, string PatientCode, string Name, string Gender,
        DateTime DateOfBirth, int Age, string Email, string ContactNumber);

    public record DeliveryDoctorDto(
        int Id, string Name, string Specialty, string PrcLicenseNumber, bool HasSignature);

    public record DeliveryParameterDto(
        string ParameterName, string Unit, string Reference, string Value, string Flag);

    public record DeliveryResultDto(
        int Id, string TestName, string Department, string CategoryName,
        string Specimen, string Method, DateTime? CollectedAt, DateTime? ReleasedAt,
        string SpecimenBarcode, string ReferringPhysician, bool IsAbnormal,
        string NarrativeFindings, string ResultedBy, string ResultKind,
        List<DeliveryParameterDto> Parameters);

    /// <summary>The clinic letterhead, sent to the client so the sheet can print it.</summary>
    public record ClinicDto(
        string Name, string Tagline, string SubTagline,
        string DohLicenseNumber, string MobileNumber, string Email, string Website);

    private record Assembled(
        DeliveryPatientDto? Patient, DeliveryDoctorDto? Doctor, List<DeliveryResultDto> Results,
        Patient? PatientEntity, IActionResult? Error);

    /// <summary>
    /// Loads the patient, the signing doctor and the chosen results, refusing
    /// anything that does not belong to this patient or is not released.
    /// </summary>
    private async Task<Assembled> AssembleAsync(int patientId, List<int> labOrderIds, int doctorId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient is null)
            return Fail(NotFound(new { message = "Patient not found." }));

        var doctor = await _db.Doctors.FindAsync(doctorId);
        if (doctor is null)
            return Fail(BadRequest(new { message = "Select an attending doctor." }));

        var ids = labOrderIds ?? new List<int>();
        var orders = ids.Count == 0
            ? new List<LabOrder>()
            : await ReleasedOrdersQuery(patientId).Where(o => ids.Contains(o.Id)).ToListAsync();

        // Silently dropping one would mean sending a package short of what was
        // ticked, and nobody would notice until the patient asked.
        if (orders.Count != ids.Count)
        {
            return Fail(BadRequest(new
            {
                message = "One of the selected results is not released, or does not belong to this patient."
            }));
        }

        var results = orders
            .OrderByDescending(o => o.ReleasedAt)
            .Select(o => new DeliveryResultDto(
                o.Id,
                o.LabTest.Name,
                o.LabTest.Category.Department,
                o.LabTest.Category.Name,
                o.LabTest.Specimen,
                o.LabTest.Method,
                o.CollectedAt,
                o.ReleasedAt,
                o.SpecimenBarcode,
                o.Visit.ReferringPhysician,
                o.IsAbnormal,
                o.NarrativeFindings,
                o.ResultedBy,
                o.LabTest.Parameters.Count > 0 ? "parameters" : "narrative",
                o.LabTest.Parameters.Select(prm =>
                {
                    var value = o.Results.FirstOrDefault(r => r.TestParameterId == prm.Id);
                    return new DeliveryParameterDto(
                        prm.Name, prm.Unit, prm.ReferenceRange,
                        value?.Value ?? "", value?.Flag ?? "");
                }).ToList()))
            .ToList();

        var patientDto = new DeliveryPatientDto(
            patient.Id, patient.PatientCode,
            $"{patient.FirstName} {patient.LastName}".Trim(),
            patient.Gender, patient.DateOfBirth,
            LabController.AgeOn(patient.DateOfBirth, DateTime.Today),
            patient.Email, patient.ContactNumber);

        var doctorDto = new DeliveryDoctorDto(
            doctor.Id,
            $"Dr. {doctor.FirstName} {doctor.LastName}".Trim(),
            doctor.Specialty, doctor.PrcLicenseNumber,
            doctor.SignatureImage != null && doctor.SignatureImage.Length > 0);

        return new Assembled(patientDto, doctorDto, results, patient, null);
    }

    private static Assembled Fail(IActionResult error) =>
        new(null, null, new List<DeliveryResultDto>(), null, error);

    private ClinicDto ClinicPayload() => new(
        _clinic.Name, _clinic.Tagline, _clinic.SubTagline,
        _clinic.DohLicenseNumber, _clinic.MobileNumber, _clinic.Email, _clinic.Website);

    /// <summary>
    /// A QR of the patient code as PNG bytes — the same code the patient card
    /// carries, drawn on the sheet for staff to scan.
    /// </summary>
    private static byte[]? GetPatientQrPng(string patientCode)
    {
        if (string.IsNullOrWhiteSpace(patientCode)) return null;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(patientCode, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(20);
    }

    private async Task<List<object>> DocumentSummariesAsync(int patientId, List<int>? documentIds)
    {
        var ids = documentIds ?? new List<int>();
        if (ids.Count == 0) return new List<object>();

        return (await _db.PatientDocuments
            .Where(d => d.PatientId == patientId && ids.Contains(d.Id))
            .Select(d => new { d.Id, d.FileName, d.Department, d.SizeBytes, resultDate = d.ResultDate })
            .ToListAsync())
            .Cast<object>()
            .ToList();
    }

    private async Task<List<EmailAttachment>> LoadAttachmentsAsync(int patientId, List<int>? documentIds)
    {
        var ids = documentIds ?? new List<int>();
        if (ids.Count == 0) return new List<EmailAttachment>();

        // Scoped to the patient, so a stray id cannot pull another patient's file.
        var documents = await _db.PatientDocuments
            .Where(d => d.PatientId == patientId && ids.Contains(d.Id))
            .ToListAsync();

        return documents
            .Select(d => new EmailAttachment(d.FileName, d.ContentType, d.Content))
            .ToList();
    }

    /// <summary>
    /// The email body — a short cover note. The result sheet itself is the PDF
    /// attachment, which is the only way to carry the watermark and the exact
    /// printed layout, so the body just introduces it.
    /// </summary>
    private static string RenderCoverNote(
        DeliveryPatientDto patient, string? message, List<string> uploadedFileNames)
    {
        const string ink = "#1b1b16";
        const string muted = "#6b6b60";

        var html = new StringBuilder();
        html.Append($"<div style=\"font-family:Arial,Helvetica,sans-serif;color:{ink};max-width:560px;margin:0 auto;font-size:14px;line-height:1.6\">");
        html.Append("<p style=\"font-size:20px;font-weight:700;letter-spacing:3px;margin:0 0 2px\">PRO-CREATE</p>");
        html.Append($"<p style=\"margin:0 0 18px;color:{muted};font-size:12px\">Fertility and OB-GYN Clinic</p>");

        html.Append($"<p>Dear {Escape(patient.Name)},</p>");
        html.Append("<p>Your results are attached to this email as a PDF. Open it to view or print "
                  + "the full report, and discuss anything you are unsure about with your doctor.</p>");

        if (!string.IsNullOrWhiteSpace(message))
        {
            html.Append($"<div style=\"margin:16px 0;padding:12px 14px;background:#F0EBE1;border-radius:6px\">");
            html.Append($"<p style=\"margin:0;white-space:pre-wrap\">{Escape(message)}</p></div>");
        }

        if (uploadedFileNames.Count > 0)
        {
            html.Append($"<p style=\"font-size:13px;color:{muted}\">Also attached: "
                      + Escape(string.Join(", ", uploadedFileNames)) + "</p>");
        }

        html.Append($"<p style=\"margin-top:26px;font-size:12px;color:#9a9a8f\">"
                  + "This is an automated message — please do not reply to this email. "
                  + "It contains personal medical information; if it reached you in error, "
                  + "please delete it and let the clinic know.</p>");
        html.Append("</div>");
        return html.ToString();
    }

    /// <summary>The body is assembled by hand, so values have to be escaped.</summary>
    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
