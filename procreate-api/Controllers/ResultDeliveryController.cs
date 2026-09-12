using System.Text;
using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public ResultDeliveryController(AppDbContext db, IEmailSender email)
    {
        _db = db;
        _email = email;
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

        var body = RenderHtml(
            package, request.IncludeSignature, request.Message,
            attachments.Select(a => a.FileName).ToList());

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
            attachments = attachments.Count
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
        DateTime DateOfBirth, int Age, string Email);

    public record DeliveryDoctorDto(int Id, string Name, string Specialty, string PrcLicenseNumber);

    public record DeliveryParameterDto(
        string ParameterName, string Unit, string Reference, string Value, string Flag);

    public record DeliveryResultDto(
        int Id, string TestName, string Department, string CategoryName,
        string Specimen, string Method, DateTime? ReleasedAt, bool IsAbnormal,
        string NarrativeFindings, string ResultedBy, string ResultKind,
        List<DeliveryParameterDto> Parameters);

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
                o.ReleasedAt,
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
            patient.Email);

        var doctorDto = new DeliveryDoctorDto(
            doctor.Id,
            $"Dr. {doctor.FirstName} {doctor.LastName}".Trim(),
            doctor.Specialty, doctor.PrcLicenseNumber);

        return new Assembled(patientDto, doctorDto, results, patient, null);
    }

    private static Assembled Fail(IActionResult error) =>
        new(null, null, new List<DeliveryResultDto>(), null, error);

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
    /// The email body. Inline styles and tables rather than a stylesheet —
    /// mail clients strip anything else.
    /// </summary>
    private static string RenderHtml(
        Assembled package, bool includeSignature, string? message, List<string> attachmentNames)
    {
        var patient = package.Patient!;
        var doctor = package.Doctor!;

        var html = new StringBuilder();

        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1E1E19;max-width:720px\">");
        html.Append("<h2 style=\"margin:0 0 4px\">Pro-Create</h2>");
        html.Append("<p style=\"margin:0 0 18px;color:#6b6b60;font-size:13px\">"
                  + "Fertility, OB-GYN and Diagnostic Center</p>");

        html.Append($"<p>Dear {Escape(patient.Name)},</p>");
        html.Append("<p>Please find your results below. Bring this with you to your next visit, "
                  + "and discuss anything you are unsure about with your doctor.</p>");

        if (!string.IsNullOrWhiteSpace(message))
        {
            html.Append("<div style=\"margin:16px 0;padding:12px 14px;background:#F0EBE1;border-radius:6px\">");
            html.Append($"<p style=\"margin:0;white-space:pre-wrap\">{Escape(message)}</p></div>");
        }

        html.Append("<table style=\"margin:18px 0;font-size:13px;color:#6b6b60\"><tr>");
        html.Append($"<td style=\"padding-right:24px\"><strong>{Escape(patient.Name)}</strong><br>"
                  + $"{Escape(patient.PatientCode)}</td>");
        html.Append($"<td>{patient.Age} years old &middot; {Escape(patient.Gender)}</td>");
        html.Append("</tr></table>");

        foreach (var result in package.Results)
        {
            html.Append("<div style=\"margin:0 0 20px;border:1px solid #e3d9c9;border-radius:8px;overflow:hidden\">");
            html.Append("<div style=\"padding:10px 14px;background:#F0EBE1\">");
            html.Append($"<strong>{Escape(result.TestName)}</strong>");

            if (result.IsAbnormal)
                html.Append(" <span style=\"color:#a71d2a;font-size:12px\">(Abnormal)</span>");

            var released = result.ReleasedAt?.ToString("d MMMM yyyy") ?? "\u2014";
            html.Append($"<div style=\"font-size:12px;color:#6b6b60\">{Escape(result.Department)} &middot; "
                      + $"Released {released}</div>");
            html.Append("</div><div style=\"padding:12px 14px\">");

            if (!string.IsNullOrWhiteSpace(result.NarrativeFindings))
            {
                html.Append("<p style=\"margin:0 0 10px;white-space:pre-wrap;line-height:1.6\">"
                          + $"{Escape(result.NarrativeFindings)}</p>");
            }

            if (result.Parameters.Count > 0)
            {
                html.Append("<table cellpadding=\"6\" cellspacing=\"0\" "
                          + "style=\"width:100%;border-collapse:collapse;font-size:13px\">");
                html.Append("<tr style=\"text-align:left;color:#6b6b60\">"
                          + "<th>Test</th><th>Result</th><th>Reference</th></tr>");

                foreach (var parameter in result.Parameters)
                {
                    // Anything the lab flagged is emphasised; "N" means normal.
                    var emphasis = string.IsNullOrWhiteSpace(parameter.Flag)
                                || parameter.Flag is "N" or "Normal"
                        ? ""
                        : "color:#a71d2a;font-weight:bold;";

                    html.Append("<tr style=\"border-top:1px solid #eee\">");
                    html.Append($"<td>{Escape(parameter.ParameterName)}</td>");
                    html.Append($"<td style=\"{emphasis}\">{Escape(parameter.Value)} "
                              + $"{Escape(parameter.Unit)} {Escape(parameter.Flag)}</td>");
                    html.Append($"<td style=\"color:#6b6b60\">{Escape(parameter.Reference)}</td>");
                    html.Append("</tr>");
                }

                html.Append("</table>");
            }

            html.Append("</div></div>");
        }

        if (attachmentNames.Count > 0)
        {
            html.Append("<p style=\"font-size:13px;color:#6b6b60\">Attached to this email: "
                      + Escape(string.Join(", ", attachmentNames)) + "</p>");
        }

        if (includeSignature)
        {
            html.Append("<div style=\"margin-top:28px\">");
            html.Append("<div style=\"border-top:1px solid #1E1E19;display:inline-block;padding-top:6px\">"
                      + $"<strong>{Escape(doctor.Name)}</strong>");
            html.Append($"<div style=\"font-size:12px;color:#6b6b60\">{Escape(doctor.Specialty)}");

            if (!string.IsNullOrWhiteSpace(doctor.PrcLicenseNumber))
                html.Append($"<br>PRC No. {Escape(doctor.PrcLicenseNumber)}");

            html.Append("</div></div></div>");
        }

        html.Append("<p style=\"margin-top:28px;font-size:12px;color:#9a9a8f\">"
                  + "This message contains personal medical information. If it reached you in error, "
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
