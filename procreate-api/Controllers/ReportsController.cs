using ProCreateApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet("visit/{visitId}/pdf")]
    public async Task<IActionResult> GenerateResultPdf(int visitId)
    {
        var visit = await _db.Visits
            .Include(v => v.Patient)
            .Include(v => v.LabOrders).ThenInclude(o => o.LabTest)
            .Include(v => v.LabOrders).ThenInclude(o => o.Results).ThenInclude(r => r.TestParameter)
            .FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit is null) return NotFound();

        using var ms = new MemoryStream();
        var writer = new PdfWriter(ms);
        var pdf = new PdfDocument(writer);
        var doc = new Document(pdf);

        var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

        // Header
        var headerTable = new Table(2).UseAllAvailableWidth();
        var titleCell = new Cell().Add(new Paragraph("PRO-CREATE").SetFont(bold).SetFontSize(20).SetFontColor(new DeviceRgb(0, 123, 255)));
        titleCell.Add(new Paragraph("Laboratory Results Report").SetFont(regular).SetFontSize(10));
        titleCell.SetBorder(iText.Layout.Borders.Border.NO_BORDER);
        headerTable.AddCell(titleCell);

        var infoCell = new Cell()
            .Add(new Paragraph($"Patient: {visit.Patient.FirstName} {visit.Patient.LastName}").SetFont(bold).SetFontSize(10))
            .Add(new Paragraph($"Code: {visit.Patient.PatientCode}").SetFont(regular).SetFontSize(9))
            .Add(new Paragraph($"Visit: {visit.VisitCode}").SetFont(regular).SetFontSize(9))
            .Add(new Paragraph($"Date: {visit.VisitDate:MMMM dd, yyyy}").SetFont(regular).SetFontSize(9))
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetTextAlignment(TextAlignment.RIGHT);
        headerTable.AddCell(infoCell);
        doc.Add(headerTable);
        doc.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine()));

        foreach (var order in visit.LabOrders.Where(o => o.Status == "Released" || o.Status == "Resulted"))
        {
            doc.Add(new Paragraph(order.LabTest.Name).SetFont(bold).SetFontSize(12).SetMarginTop(10));

            var table = new Table(new float[] { 3, 2, 2, 2 }).UseAllAvailableWidth();
            foreach (var h in new[] { "Parameter", "Result", "Reference Range", "Flag" })
                table.AddHeaderCell(new Cell().Add(new Paragraph(h).SetFont(bold).SetFontSize(9))
                    .SetBackgroundColor(new DeviceRgb(0, 123, 255)).SetFontColor(ColorConstants.WHITE));

            foreach (var result in order.Results)
            {
                var flagColor = result.Flag == "Normal" ? ColorConstants.BLACK : new DeviceRgb(220, 53, 69);
                table.AddCell(new Cell().Add(new Paragraph(result.TestParameter.Name).SetFont(regular).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(result.Value).SetFont(bold).SetFontSize(9).SetFontColor(flagColor)));
                table.AddCell(new Cell().Add(new Paragraph(result.TestParameter.ReferenceRange).SetFont(regular).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(result.Flag).SetFont(regular).SetFontSize(9).SetFontColor(flagColor)));
            }
            doc.Add(table);
        }

        doc.Add(new Paragraph("\n\nAuthorized by: ___________________________")
            .SetFont(regular).SetFontSize(9).SetMarginTop(30).SetTextAlignment(TextAlignment.RIGHT));
        doc.Add(new Paragraph("Medical Technologist / Pathologist")
            .SetFont(regular).SetFontSize(9).SetTextAlignment(TextAlignment.RIGHT));

        doc.Close();
        return File(ms.ToArray(), "application/pdf", $"results-{visit.VisitCode}.pdf");
    }

    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyReport([FromQuery] DateTime? date)
    {
        var reportDate = (date ?? DateTime.UtcNow).Date;
        var visits = await _db.Visits.Include(v => v.Patient).Include(v => v.LabOrders)
            .Where(v => v.VisitDate.Date == reportDate).ToListAsync();
        var bills = await _db.Bills.Where(b => b.CreatedAt.Date == reportDate && b.Status == "Paid").ToListAsync();

        return Ok(new
        {
            date = reportDate,
            totalPatients = visits.Select(v => v.PatientId).Distinct().Count(),
            totalVisits = visits.Count,
            totalTests = visits.Sum(v => v.LabOrders.Count),
            totalRevenue = bills.Sum(b => b.Total),
            pendingResults = visits.Sum(v => v.LabOrders.Count(o => o.Status == "Ordered" || o.Status == "Collected")),
            completedVisits = visits.Count(v => v.Status == "Completed"),
            visitsByStatus = visits.GroupBy(v => v.Status).Select(g => new { status = g.Key, count = g.Count() }),
            visits = visits.Select(v => new
            {
                v.Id,
                v.VisitCode,
                patientName = $"{v.Patient.FirstName} {v.Patient.LastName}".Trim(),
                testsCount = v.LabOrders.Count,
                v.TotalAmount,
                v.Status,
                v.PaymentStatus
            })
        });
    }
}
