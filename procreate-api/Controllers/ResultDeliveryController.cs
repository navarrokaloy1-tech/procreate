using System.Text;
using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Clinic;
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
    private readonly ClinicSettings _clinic;

    public ResultDeliveryController(AppDbContext db, IEmailSender email, ClinicSettings clinic)
    {
        _db = db;
        _email = email;
        _clinic = clinic;
    }

    /// <summary>What the embedded signature is referred to as inside the body.</summary>
    private const string SignatureContentId = "procreate-signature";

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

        // Listed before the signature is added, so the embedded image is not
        // announced to the patient as though it were a file they can open.
        var fileNames = attachments.Select(a => a.FileName).ToList();

        if (request.IncludeSignature && package.Doctor!.HasSignature)
        {
            var signature = await _db.Doctors
                .AsNoTracking()
                .Where(d => d.Id == request.DoctorId)
                .Select(d => new { d.SignatureImage, d.SignatureContentType })
                .FirstOrDefaultAsync();

            if (signature?.SignatureImage is { Length: > 0 })
            {
                attachments.Add(new EmailAttachment(
                    "signature", signature.SignatureContentType,
                    signature.SignatureImage, SignatureContentId));
            }
        }

        var body = RenderHtml(_clinic, package, request.IncludeSignature, request.Message, fileNames);

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
            // The signature rides along as an embedded image, so it is not one
            // of the files the patient receives.
            attachments = fileNames.Count
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
    /// The email body — the clinic result sheet, laid out to match the printed
    /// one. Inline styles and tables throughout, because mail clients strip a
    /// stylesheet, drop background images (so no watermark here), and Outlook
    /// will not render webp — so the letterhead is set in type rather than as
    /// the logo image the printed sheet carries.
    /// </summary>
    private static string RenderHtml(
        ClinicSettings clinic, Assembled package, bool includeSignature,
        string? message, List<string> attachmentNames)
    {
        var patient = package.Patient!;
        var doctor = package.Doctor!;
        var results = package.Results;

        const string ink = "#1b1b16";
        const string muted = "#6b6b60";
        const string rule = "#1b3a5c";
        const string danger = "#a71d2a";

        var html = new StringBuilder();
        html.Append($"<div style=\"font-family:Arial,Helvetica,sans-serif;color:{ink};max-width:760px;margin:0 auto\">");

        // ---- Letterhead ----------------------------------------------------
        html.Append("<div style=\"text-align:center\">");
        html.Append($"<div style=\"font-size:34px;font-weight:700;letter-spacing:6px\">{Escape(clinic.Name.Length > 0 ? "PRO-CREATE" : "PRO-CREATE")}</div>");
        if (!string.IsNullOrWhiteSpace(clinic.Tagline))
            html.Append($"<div style=\"font-size:13px;letter-spacing:5px;text-transform:uppercase;margin-top:2px\">{Escape(clinic.Tagline)}</div>");
        if (!string.IsNullOrWhiteSpace(clinic.SubTagline))
            html.Append($"<div style=\"font-size:10px;letter-spacing:3px;text-transform:uppercase;color:{muted}\">{Escape(clinic.SubTagline)}</div>");

        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(clinic.DohLicenseNumber)) contact.Add($"DOH License No. {Escape(clinic.DohLicenseNumber)}");
        if (!string.IsNullOrWhiteSpace(clinic.MobileNumber)) contact.Add($"Mobile No. {Escape(clinic.MobileNumber)}");
        if (!string.IsNullOrWhiteSpace(clinic.Email)) contact.Add($"E-Mail Add. {Escape(clinic.Email)}");
        if (contact.Count > 0)
            html.Append($"<div style=\"font-size:12px;color:{muted};margin-top:8px;line-height:1.5\">{string.Join("<br>", contact)}</div>");
        html.Append("</div>");

        // The double rule the printed sheet has under the letterhead.
        html.Append($"<div style=\"border-top:3px solid {rule};border-bottom:1px solid {rule};height:3px;margin:14px 0 0\"></div>");

        // ---- Optional note from the sender --------------------------------
        if (!string.IsNullOrWhiteSpace(message))
        {
            html.Append($"<div style=\"margin:16px 0;padding:12px 14px;background:#F0EBE1;border-radius:6px\">");
            html.Append($"<p style=\"margin:0;white-space:pre-wrap\">{Escape(message)}</p></div>");
        }

        // ---- Patient block -------------------------------------------------
        var collected = results.Where(r => r.CollectedAt.HasValue).Select(r => r.CollectedAt!.Value).DefaultIfEmpty().Min();
        var released = results.Where(r => r.ReleasedAt.HasValue).Select(r => r.ReleasedAt!.Value).DefaultIfEmpty().Max();
        var barcode = results.Select(r => r.SpecimenBarcode).FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";
        var referring = results.Select(r => r.ReferringPhysician).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "";

        string Cell(string label, string value) =>
            $"<div style=\"margin-bottom:3px\"><span style=\"color:{muted}\">{Escape(label)}</span> "
            + $"<strong>{Escape(value)}</strong></div>";

        html.Append("<table style=\"width:100%;font-size:12px;margin:14px 0 0;border-collapse:collapse\"><tr style=\"vertical-align:top\">");
        html.Append("<td style=\"width:40%;padding-right:12px\">");
        html.Append(Cell("Patient Name:", patient.Name));
        html.Append(Cell("Patient ID No:", patient.PatientCode));
        html.Append(Cell("Referring MD:", referring.Length > 0 ? referring : "—"));
        html.Append("</td><td style=\"width:30%;padding-right:12px\">");
        html.Append(Cell("Phone#:", string.IsNullOrWhiteSpace(patient.ContactNumber) ? "—" : patient.ContactNumber));
        html.Append(Cell("Age:", $"{patient.Age}"));
        html.Append(Cell("Birthdate:", patient.DateOfBirth.ToString("d MMM yyyy")));
        html.Append(Cell("Gender:", string.IsNullOrWhiteSpace(patient.Gender) ? "—" : patient.Gender));
        html.Append("</td><td style=\"width:30%\">");
        html.Append(Cell("Collected:", collected == default ? "—" : collected.ToString("d MMM yyyy")));
        html.Append(Cell("Release date:", released == default ? "—" : released.ToString("d MMM yyyy")));
        html.Append(Cell("Barcode:", barcode.Length > 0 ? barcode : "—"));
        html.Append("</td></tr></table>");

        html.Append($"<div style=\"border-top:3px solid {rule};border-bottom:1px solid {rule};height:3px;margin:8px 0 0\"></div>");

        // ---- Results, grouped by section ----------------------------------
        var sections = results
            .GroupBy(r => string.IsNullOrWhiteSpace(r.CategoryName) ? r.Department : r.CategoryName);

        foreach (var section in sections)
        {
            html.Append($"<div style=\"text-align:center;font-weight:700;letter-spacing:1px;text-transform:uppercase;margin:22px 0 4px;font-size:15px\">{Escape(section.Key)}</div>");

            html.Append("<table cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;border-collapse:collapse;font-size:13px\">");
            html.Append($"<tr style=\"text-align:left;font-weight:700\">"
                      + "<td style=\"width:40%;padding:4px 0\">TEST</td>"
                      + "<td style=\"width:18%;padding:4px 0\">RESULT</td>"
                      + "<td style=\"width:14%;padding:4px 0\">UNIT</td>"
                      + "<td style=\"width:28%;padding:4px 0\">REF. RANGE</td></tr>");

            foreach (var result in section)
            {
                html.Append($"<tr><td colspan=\"4\" style=\"font-weight:700;text-transform:uppercase;padding:8px 0 2px\">{Escape(result.TestName)}</td></tr>");

                if (!string.IsNullOrWhiteSpace(result.NarrativeFindings))
                {
                    html.Append($"<tr><td colspan=\"4\" style=\"padding:0 0 6px 14px;white-space:pre-wrap;line-height:1.6\">{Escape(result.NarrativeFindings)}</td></tr>");
                }

                foreach (var p in result.Parameters)
                {
                    var (mark, abnormal) = FlagMark(p.Flag);
                    var valueStyle = abnormal ? $"color:{danger};font-weight:700" : "";
                    var markSpan = mark.Length > 0 ? $"<span style=\"color:{danger}\">{mark}</span> " : "";
                    var reference = Escape(p.Reference).Replace("\n", "<br>");

                    html.Append("<tr>");
                    html.Append($"<td style=\"padding:2px 0 2px 14px\">{Escape(p.ParameterName)}</td>");
                    html.Append($"<td style=\"padding:2px 0;{valueStyle}\">{markSpan}{Escape(p.Value)}</td>");
                    html.Append($"<td style=\"padding:2px 0;color:{muted}\">{Escape(p.Unit)}</td>");
                    html.Append($"<td style=\"padding:2px 0;color:{muted}\">{(reference.Length > 0 ? reference : "—")}</td>");
                    html.Append("</tr>");
                }
            }

            html.Append("</table>");
        }

        html.Append($"<div style=\"text-align:center;color:{muted};font-size:12px;margin:18px 0\">- End of Report -</div>");

        if (attachmentNames.Count > 0)
        {
            html.Append($"<p style=\"font-size:12px;color:{muted}\">Attached to this email: "
                      + Escape(string.Join(", ", attachmentNames)) + "</p>");
        }

        // ---- Signatures ----------------------------------------------------
        if (includeSignature)
        {
            var medtech = results.Select(r => r.ResultedBy).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

            html.Append("<table style=\"width:100%;margin-top:30px;font-size:12px\"><tr style=\"vertical-align:bottom\">");

            // Left — the med-tech who resulted it. No stored image, so a ruled
            // name only.
            html.Append("<td style=\"width:50%;text-align:center\">");
            if (medtech.Length > 0)
            {
                html.Append($"<div style=\"border-top:1px solid {ink};display:inline-block;padding-top:4px;min-width:200px\">"
                          + $"<strong>{Escape(medtech)}</strong>"
                          + $"<div style=\"color:{muted}\">Medical Technologist</div></div>");
            }
            html.Append("</td>");

            // Right — the signing doctor, with their scanned signature above the
            // rule (referenced by cid, the only embedded image mail reliably shows).
            html.Append("<td style=\"width:50%;text-align:center\">");
            if (doctor.HasSignature)
                html.Append($"<img src=\"cid:{SignatureContentId}\" alt=\"\" style=\"display:block;max-height:60px;margin:0 auto 2px\">");
            html.Append($"<div style=\"border-top:1px solid {ink};display:inline-block;padding-top:4px;min-width:200px\">"
                      + $"<strong>{Escape(doctor.Name)}</strong>");
            if (!string.IsNullOrWhiteSpace(doctor.Specialty))
                html.Append($"<div style=\"color:{muted}\">{Escape(doctor.Specialty)}</div>");
            if (!string.IsNullOrWhiteSpace(doctor.PrcLicenseNumber))
                html.Append($"<div style=\"color:{muted}\">PRC No. {Escape(doctor.PrcLicenseNumber)}</div>");
            html.Append("</div></td>");

            html.Append("</tr></table>");
        }

        // ---- Footer --------------------------------------------------------
        html.Append($"<div style=\"text-align:center;border-top:1px dashed {muted};margin-top:26px;padding-top:10px;"
                  + $"font-style:italic;font-size:11px;color:{muted}\">This is an electronically generated report.</div>");
        html.Append($"<p style=\"margin-top:14px;font-size:11px;color:#9a9a8f\">"
                  + "This is an automated message — please do not reply to this email. "
                  + "It contains personal medical information; if it reached you in error, "
                  + "please delete it and let the clinic know.</p>");

        html.Append("</div>");
        return html.ToString();
    }

    /// <summary>
    /// Turns a lab flag into a coloured marker. A high value gets an up
    /// triangle, a low value a down one, matching how the printed sheet reads;
    /// anything normal or unflagged gets nothing.
    /// </summary>
    private static (string Mark, bool Abnormal) FlagMark(string? flag)
    {
        var f = (flag ?? string.Empty).Trim();
        if (f.Length == 0 || f is "N" or "Normal") return ("", false);
        var first = char.ToUpperInvariant(f[0]);
        if (first == 'H') return ("▲", true); // ▲
        if (first == 'L') return ("▼", true); // ▼
        return ("▲", true);
    }

    /// <summary>The body is assembled by hand, so values have to be escaped.</summary>
    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
