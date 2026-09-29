using ProCreateApi.Controllers;
using ProCreateApi.Services.Clinic;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Event;
using iText.Kernel.Pdf.Extgstate;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using Path = System.IO.Path;

namespace ProCreateApi.Services.Pdf;

/// <summary>
/// Renders a patient's result sheet to PDF, matching the printed and previewed
/// layout — letterhead, patient QR, a faint emblem watermark on every page,
/// results grouped into sections with out-of-range values flagged, and two
/// signatories. This is what an emailed result carries, because a mail body
/// cannot show a watermark and will not reliably lay a document out.
/// </summary>
public class ResultSheetPdf
{
    private readonly string _assetsPath;

    public ResultSheetPdf(IWebHostEnvironment env)
    {
        _assetsPath = Path.Combine(env.ContentRootPath, "Assets");
    }

    // Document colours, matching the on-screen sheet.
    private static readonly Color Ink = new DeviceRgb(0x1b, 0x1b, 0x16);
    private static readonly Color Muted = new DeviceRgb(0x6b, 0x6b, 0x60);
    private static readonly Color Rule = new DeviceRgb(0x1b, 0x3a, 0x5c);
    private static readonly Color Danger = new DeviceRgb(0xa7, 0x1d, 0x2a);

    public byte[] Build(
        ClinicSettings clinic,
        ResultDeliveryController.DeliveryPatientDto patient,
        ResultDeliveryController.DeliveryDoctorDto doctor,
        IReadOnlyList<ResultDeliveryController.DeliveryResultDto> results,
        string medTech,
        bool includeSignature,
        byte[]? qrPng,
        byte[]? signatureImage)
    {
        using var ms = new MemoryStream();
        using var writer = new PdfWriter(ms);
        using var pdf = new PdfDocument(writer);

        var regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var italic = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);

        // The watermark is painted behind every page by an event handler.
        var emblemPath = Path.Combine(_assetsPath, "logo.png");
        if (File.Exists(emblemPath))
            pdf.AddEventHandler(PdfDocumentEvent.END_PAGE,
                new WatermarkHandler(ImageDataFactory.Create(emblemPath)));

        var doc = new Document(pdf, PageSize.A4);
        doc.SetMargins(32, 36, 32, 36);

        AddLetterhead(doc, clinic, regular, bold, qrPng);
        AddRule(doc);
        AddPatientBlock(doc, patient, results, regular, bold);
        AddRule(doc);
        AddSections(doc, results, regular, bold);

        doc.Add(new Paragraph("- End of Report -")
            .SetFont(regular).SetFontSize(9).SetFontColor(Muted)
            .SetTextAlignment(TextAlignment.CENTER).SetMarginTop(14).SetMarginBottom(10));

        if (includeSignature)
            AddSignatures(doc, doctor, medTech, signatureImage, regular, bold);

        doc.Add(new Paragraph("This is an electronically generated report.")
            .SetFont(italic).SetFontSize(8).SetFontColor(Muted)
            .SetTextAlignment(TextAlignment.CENTER).SetMarginTop(24)
            .SetBorderTop(new DashedBorder(Muted, 0.5f)).SetPaddingTop(8));

        doc.Close();
        return ms.ToArray();
    }

    private void AddLetterhead(
        Document doc, ClinicSettings clinic, PdfFont regular, PdfFont bold, byte[]? qrPng)
    {
        var header = new Table(UnitValue.CreatePercentArray(new float[] { 72, 28 }))
            .UseAllAvailableWidth();
        header.SetBorder(Border.NO_BORDER);

        var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(0);
        var logoPath = Path.Combine(_assetsPath, "cropped-Pro-create-logo.jpg");
        if (File.Exists(logoPath))
        {
            var logo = new Image(ImageDataFactory.Create(logoPath));
            logo.SetWidth(UnitValue.CreatePercentValue(78));
            logoCell.Add(logo);
        }
        else
        {
            logoCell.Add(new Paragraph("PRO-CREATE").SetFont(bold).SetFontSize(26));
        }
        header.AddCell(logoCell);

        var qrCell = new Cell().SetBorder(Border.NO_BORDER)
            .SetTextAlignment(TextAlignment.RIGHT).SetPadding(0);
        if (qrPng is { Length: > 0 })
        {
            var qr = new Image(ImageDataFactory.Create(qrPng));
            qr.SetWidth(72);
            qrCell.Add(qr);
        }
        header.AddCell(qrCell);
        doc.Add(header);

        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(clinic.DohLicenseNumber)) contact.Add($"DOH License No. {clinic.DohLicenseNumber}");
        if (!string.IsNullOrWhiteSpace(clinic.MobileNumber)) contact.Add($"Mobile No. {clinic.MobileNumber}");
        if (!string.IsNullOrWhiteSpace(clinic.Email)) contact.Add($"E-Mail Add. {clinic.Email}");
        if (contact.Count > 0)
            doc.Add(new Paragraph(string.Join("\n", contact))
                .SetFont(regular).SetFontSize(9).SetFontColor(Ink)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginTop(6).SetMultipliedLeading(1.3f));
    }

    private static void AddRule(Document doc)
    {
        // The double rule the sheet carries: a thick line over a thin one.
        var thick = new Table(1).UseAllAvailableWidth().SetMarginTop(8);
        thick.SetBorder(Border.NO_BORDER);
        thick.AddCell(new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetBorderTop(new SolidBorder(Rule, 2.5f))
            .SetBorderBottom(new SolidBorder(Rule, 0.75f))
            .SetHeight(3).SetPadding(0));
        doc.Add(thick);
    }

    private void AddPatientBlock(
        Document doc,
        ResultDeliveryController.DeliveryPatientDto patient,
        IReadOnlyList<ResultDeliveryController.DeliveryResultDto> results,
        PdfFont regular, PdfFont bold)
    {
        var collected = results.Where(r => r.CollectedAt.HasValue)
            .Select(r => r.CollectedAt!.Value).DefaultIfEmpty().Min();
        var released = results.Where(r => r.ReleasedAt.HasValue)
            .Select(r => r.ReleasedAt!.Value).DefaultIfEmpty().Max();
        var barcode = results.Select(r => r.SpecimenBarcode).FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";
        var referring = results.Select(r => r.ReferringPhysician).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "";

        var table = new Table(UnitValue.CreatePercentArray(new float[] { 40, 30, 30 }))
            .UseAllAvailableWidth().SetMarginTop(8);
        table.SetBorder(Border.NO_BORDER);

        Cell Col() => new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).SetPaddingRight(8);

        Paragraph Field(string label, string value) =>
            new Paragraph()
                .Add(new Text(label + " ").SetFont(regular).SetFontColor(Muted))
                .Add(new Text(value).SetFont(bold).SetFontColor(Ink))
                .SetFontSize(9).SetMarginBottom(2).SetMultipliedLeading(1.15f);

        var c1 = Col();
        c1.Add(Field("Patient Name:", patient.Name));
        c1.Add(Field("Patient ID No:", patient.PatientCode));
        c1.Add(Field("Referring MD:", referring.Length > 0 ? referring : "—"));
        table.AddCell(c1);

        var c2 = Col();
        c2.Add(Field("Phone#:", string.IsNullOrWhiteSpace(patient.ContactNumber) ? "—" : patient.ContactNumber));
        c2.Add(Field("Age:", patient.Age.ToString()));
        c2.Add(Field("Birthdate:", patient.DateOfBirth.ToString("MMM d, yyyy")));
        c2.Add(Field("Gender:", string.IsNullOrWhiteSpace(patient.Gender) ? "—" : patient.Gender));
        table.AddCell(c2);

        var c3 = Col();
        c3.Add(Field("Collected:", collected == default ? "—" : collected.ToString("MMM d, yyyy")));
        c3.Add(Field("Release date:", released == default ? "—" : released.ToString("MMM d, yyyy")));
        c3.Add(Field("Barcode:", barcode.Length > 0 ? barcode : "—"));
        table.AddCell(c3);

        doc.Add(table);
    }

    private void AddSections(
        Document doc,
        IReadOnlyList<ResultDeliveryController.DeliveryResultDto> results,
        PdfFont regular, PdfFont bold)
    {
        var order = new List<string>();
        var byTitle = new Dictionary<string, List<ResultDeliveryController.DeliveryResultDto>>();
        foreach (var r in results)
        {
            var title = (string.IsNullOrWhiteSpace(r.CategoryName) ? r.Department : r.CategoryName).ToUpperInvariant();
            if (!byTitle.ContainsKey(title)) { byTitle[title] = new(); order.Add(title); }
            byTitle[title].Add(r);
        }

        foreach (var title in order)
        {
            doc.Add(new Paragraph(title)
                .SetFont(bold).SetFontSize(11).SetFontColor(Ink)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(16).SetMarginBottom(4).SetCharacterSpacing(0.5f));

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 40, 18, 14, 28 }))
                .UseAllAvailableWidth();
            table.SetBorder(Border.NO_BORDER);

            foreach (var h in new[] { "TEST", "RESULT", "UNIT", "REF. RANGE" })
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(h).SetFont(bold).SetFontSize(9))
                    .SetBorder(Border.NO_BORDER)
                    .SetBorderBottom(new SolidBorder(Ink, 1f))
                    .SetPadding(3));

            foreach (var result in byTitle[title])
            {
                table.AddCell(GroupCell(result.TestName, bold));

                if (!string.IsNullOrWhiteSpace(result.NarrativeFindings))
                    table.AddCell(new Cell(1, 4)
                        .Add(new Paragraph(result.NarrativeFindings)
                            .SetFont(regular).SetFontSize(9).SetMultipliedLeading(1.3f))
                        .SetBorder(Border.NO_BORDER).SetPaddingLeft(14).SetPaddingBottom(4));

                foreach (var p in result.Parameters)
                {
                    var (mark, abnormal) = FlagMark(p.Flag);

                    table.AddCell(Body(p.ParameterName, regular).SetPaddingLeft(14));

                    var value = new Paragraph().SetFontSize(9).SetMultipliedLeading(1.15f);
                    if (mark.Length > 0) value.Add(new Text(mark + " ").SetFont(bold).SetFontColor(Danger));
                    value.Add(new Text(p.Value ?? "").SetFont(abnormal ? bold : regular)
                        .SetFontColor(abnormal ? Danger : Ink));
                    table.AddCell(new Cell().Add(value).SetBorder(Border.NO_BORDER).SetPadding(2));

                    table.AddCell(Body(p.Unit, regular).SetFontColor(Muted));
                    table.AddCell(RefCell(p.Reference, regular));
                }
            }

            doc.Add(table);
        }
    }

    private static Cell GroupCell(string name, PdfFont bold) =>
        new Cell(1, 4)
            .Add(new Paragraph((name ?? "").ToUpperInvariant()).SetFont(bold).SetFontSize(9))
            .SetBorder(Border.NO_BORDER).SetPaddingTop(8).SetPaddingBottom(1);

    private static Cell Body(string? text, PdfFont font) =>
        new Cell()
            .Add(new Paragraph(text ?? "").SetFont(font).SetFontSize(9).SetMultipliedLeading(1.15f))
            .SetBorder(Border.NO_BORDER).SetPadding(2);

    private static Cell RefCell(string? reference, PdfFont font)
    {
        var cell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(2);
        var lines = (reference ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0)
        {
            cell.Add(new Paragraph("—").SetFont(font).SetFontSize(9).SetFontColor(Muted));
        }
        else
        {
            foreach (var line in lines)
                cell.Add(new Paragraph(line).SetFont(font).SetFontSize(8.5f).SetFontColor(Muted).SetMultipliedLeading(1.1f));
        }
        return cell;
    }

    private void AddSignatures(
        Document doc,
        ResultDeliveryController.DeliveryDoctorDto doctor,
        string medTech, byte[]? signatureImage,
        PdfFont regular, PdfFont bold)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
            .UseAllAvailableWidth().SetMarginTop(36);
        table.SetBorder(Border.NO_BORDER);

        // Left — med-tech, an empty signature slot then a ruled name.
        var left = new Cell().SetBorder(Border.NO_BORDER)
            .SetTextAlignment(TextAlignment.CENTER).SetPaddingTop(46);
        left.Add(new Paragraph(string.IsNullOrWhiteSpace(medTech) ? "—" : medTech)
            .SetFont(bold).SetFontSize(9.5f)
            .SetBorderTop(new SolidBorder(Ink, 0.75f)).SetPaddingTop(4)
            .SetMarginLeft(20).SetMarginRight(20));
        left.Add(new Paragraph("Medical Technologist").SetFont(regular).SetFontSize(8.5f).SetFontColor(Muted));
        table.AddCell(left);

        // Right — signing doctor, scanned signature above the rule.
        var right = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
        if (signatureImage is { Length: > 0 })
        {
            try
            {
                var sig = new Image(ImageDataFactory.Create(signatureImage));
                sig.SetAutoScaleHeight(true).SetHeight(44)
                    .SetHorizontalAlignment(HorizontalAlignment.CENTER);
                right.Add(sig);
            }
            catch
            {
                right.SetPaddingTop(46);
            }
        }
        else
        {
            right.SetPaddingTop(46);
        }
        right.Add(new Paragraph(doctor.Name).SetFont(bold).SetFontSize(9.5f)
            .SetBorderTop(new SolidBorder(Ink, 0.75f)).SetPaddingTop(4)
            .SetMarginLeft(20).SetMarginRight(20));
        if (!string.IsNullOrWhiteSpace(doctor.Specialty))
            right.Add(new Paragraph(doctor.Specialty).SetFont(regular).SetFontSize(8.5f).SetFontColor(Muted));
        if (!string.IsNullOrWhiteSpace(doctor.PrcLicenseNumber))
            right.Add(new Paragraph($"PRC No. {doctor.PrcLicenseNumber}").SetFont(regular).SetFontSize(8.5f).SetFontColor(Muted));
        table.AddCell(right);

        doc.Add(table);
    }

    /// <summary>▲ for a high value, ▼ for a low one; nothing when in range.</summary>
    private static (string Mark, bool Abnormal) FlagMark(string? flag)
    {
        var f = (flag ?? "").Trim();
        if (f.Length == 0 || f is "N" or "Normal") return ("", false);
        var first = char.ToUpperInvariant(f[0]);
        if (first == 'L') return ("▼", true);
        return ("▲", true);
    }

    /// <summary>Paints the faint emblem behind the content on every page.</summary>
    private sealed class WatermarkHandler : AbstractPdfDocumentEventHandler
    {
        private readonly ImageData _emblem;
        public WatermarkHandler(ImageData emblem) => _emblem = emblem;

        protected override void OnAcceptedEvent(AbstractPdfDocumentEvent @event)
        {
            if (@event is not PdfDocumentEvent docEvent) return;
            var page = docEvent.GetPage();
            var size = page.GetPageSize();

            var canvas = new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), docEvent.GetDocument());
            canvas.SaveState();
            var gs = new PdfExtGState().SetFillOpacity(0.06f);
            canvas.SetExtGState(gs);

            var width = size.GetWidth() * 0.6f;
            var height = width * _emblem.GetHeight() / _emblem.GetWidth();
            var x = (size.GetWidth() - width) / 2;
            var y = (size.GetHeight() - height) / 2;

            canvas.AddImageFittedIntoRectangle(_emblem, new Rectangle(x, y, width, height), false);
            canvas.RestoreState();
        }
    }
}
