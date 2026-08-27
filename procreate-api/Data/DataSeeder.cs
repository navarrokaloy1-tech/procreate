using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Data;

/// <summary>
/// Seeds realistic transactional sample data (patients, visits, lab orders,
/// results and bills) the first time the database is empty, so the Visits,
/// Lab Results, Billing and Reports screens are populated out of the box.
/// Idempotent: does nothing once visits already exist.
/// </summary>
public static class DataSeeder
{
    public static void SeedSampleData(AppDbContext db)
    {
        if (db.Visits.Any()) return;

        var today = DateTime.UtcNow;
        string Stamp(int i) => today.ToString("yyyyMMdd");

        // ---------- Patients ----------
        var maria = new Patient
        {
            PatientCode = $"PT-{Stamp(1)}-0001", FirstName = "Maria", MiddleName = "Cruz", LastName = "Santos",
            DateOfBirth = new DateTime(1990, 5, 14), Gender = "Female", ContactNumber = "09171234567",
            Email = "maria.santos@example.com", Address = "12 Mabini St, Makati City", BloodType = "O+",
            EmergencyContactName = "Jose Santos", EmergencyContactNumber = "09181234567", CreatedAt = today.AddHours(-5)
        };
        var juan = new Patient
        {
            PatientCode = $"PT-{Stamp(2)}-0002", FirstName = "Juan", MiddleName = "", LastName = "Dela Cruz",
            DateOfBirth = new DateTime(1985, 11, 2), Gender = "Male", ContactNumber = "09221234567",
            Email = "juan.delacruz@example.com", Address = "45 Rizal Ave, Quezon City", BloodType = "A+",
            CreatedAt = today.AddHours(-4)
        };
        var ana = new Patient
        {
            PatientCode = $"PT-{Stamp(3)}-0003", FirstName = "Ana", MiddleName = "Reyes", LastName = "Lim",
            DateOfBirth = new DateTime(1998, 2, 28), Gender = "Female", ContactNumber = "09331234567",
            Email = "ana.lim@example.com", Address = "8 Bonifacio St, Pasig City", BloodType = "B+",
            CreatedAt = today.AddHours(-2)
        };
        db.Patients.AddRange(maria, juan, ana);
        db.SaveChanges();

        var cbc = db.LabTests.First(t => t.Code == "CBC");
        var fbs = db.LabTests.First(t => t.Code == "FBS");
        var crea = db.LabTests.First(t => t.Code == "CREA");
        var ua = db.LabTests.First(t => t.Code == "UA");

        // ---------- Visit 1: Maria — Completed, results released, paid ----------
        var v1 = new Visit
        {
            VisitCode = $"VS-{Stamp(1)}-0001", PatientId = maria.Id, VisitDate = today.AddHours(-5),
            ReferringPhysician = "Dr. Reyes", Purpose = "Annual physical exam", Status = "Completed",
            TotalAmount = cbc.Price + fbs.Price, AmountPaid = cbc.Price + fbs.Price,
            PaymentStatus = "Paid", PaymentMethod = "Cash"
        };
        db.Visits.Add(v1);
        db.SaveChanges();

        var o1 = new LabOrder
        {
            VisitId = v1.Id, LabTestId = cbc.Id, Status = "Released",
            SpecimenBarcode = $"SPX-{Stamp(1)}-001", CollectedAt = today.AddHours(-5),
            ProcessedAt = today.AddHours(-4), ReleasedAt = today.AddHours(-3)
        };
        var o2 = new LabOrder
        {
            VisitId = v1.Id, LabTestId = fbs.Id, Status = "Released",
            SpecimenBarcode = $"SPX-{Stamp(1)}-002", CollectedAt = today.AddHours(-5),
            ProcessedAt = today.AddHours(-4), ReleasedAt = today.AddHours(-3)
        };
        db.LabOrders.AddRange(o1, o2);
        db.SaveChanges();

        var cbcParams = db.TestParameters.Where(p => p.LabTestId == cbc.Id).OrderBy(p => p.Id).ToList();
        var fbsParam = db.TestParameters.First(p => p.LabTestId == fbs.Id);
        string[] cbcValues = { "7.2", "5.0", "15.1", "46", "270" };
        for (int i = 0; i < cbcParams.Count && i < cbcValues.Length; i++)
        {
            db.LabResults.Add(new LabResult { LabOrderId = o1.Id, TestParameterId = cbcParams[i].Id, Value = cbcValues[i], Flag = "Normal" });
        }
        db.LabResults.Add(new LabResult { LabOrderId = o2.Id, TestParameterId = fbsParam.Id, Value = "112", Flag = "High", Remarks = "Slightly elevated; advise fasting recheck." });

        db.Bills.Add(new Bill
        {
            BillNumber = $"BL-{Stamp(1)}-0001", VisitId = v1.Id, SubTotal = cbc.Price + fbs.Price, Discount = 0,
            Total = cbc.Price + fbs.Price, AmountPaid = cbc.Price + fbs.Price, Change = 0,
            PaymentMethod = "Cash", Status = "Paid", CreatedAt = today.AddHours(-3)
        });

        // ---------- Visit 2: Juan — Processing, specimens collected, awaiting results ----------
        var v2 = new Visit
        {
            VisitCode = $"VS-{Stamp(2)}-0002", PatientId = juan.Id, VisitDate = today.AddHours(-4),
            ReferringPhysician = "Dr. Tan", Purpose = "Kidney function check", Status = "Processing",
            TotalAmount = crea.Price + ua.Price, AmountPaid = 0, PaymentStatus = "Unpaid"
        };
        db.Visits.Add(v2);
        db.SaveChanges();
        db.LabOrders.AddRange(
            new LabOrder { VisitId = v2.Id, LabTestId = crea.Id, Status = "Collected", SpecimenBarcode = $"SPX-{Stamp(2)}-003", CollectedAt = today.AddHours(-3) },
            new LabOrder { VisitId = v2.Id, LabTestId = ua.Id, Status = "Collected", SpecimenBarcode = $"SPX-{Stamp(2)}-004", CollectedAt = today.AddHours(-3) }
        );

        // ---------- Visit 3: Ana — Registered, freshly ordered ----------
        var v3 = new Visit
        {
            VisitCode = $"VS-{Stamp(3)}-0003", PatientId = ana.Id, VisitDate = today.AddHours(-1),
            ReferringPhysician = "Dr. Cruz", Purpose = "Pre-employment screening", Status = "Registered",
            TotalAmount = cbc.Price, AmountPaid = 0, PaymentStatus = "Unpaid"
        };
        db.Visits.Add(v3);
        db.SaveChanges();
        db.LabOrders.Add(new LabOrder { VisitId = v3.Id, LabTestId = cbc.Id, Status = "Ordered", SpecimenBarcode = $"SPX-{Stamp(3)}-005" });

        db.SaveChanges();

        // ---------- Diagnostics outside the lab bench ----------
        SeedDiagnosticStudies(db, maria, juan, today);

        // ---------- Clinical chart for the first patient ----------
        SeedChart(db, maria, today);

        // ---------- Cashier Orders (Product/Order domain) ----------
        SeedOrders(db, juan, today);
    }

    /// <summary>
    /// Imaging, ultrasound and heart-station orders, so the Laboratory console
    /// has something under every department tab. These studies carry no test
    /// parameters, so the reading is stored as narrative findings.
    /// </summary>
    private static void SeedDiagnosticStudies(AppDbContext db, Patient maria, Patient juan, DateTime today)
    {
        string Stamp() => today.ToString("yyyyMMdd");

        var chestXray = db.LabTests.First(t => t.Code == "CXR-APL");
        var breastUs = db.LabTests.First(t => t.Code == "BUS");
        var ecg = db.LabTests.First(t => t.Code == "ECG");

        // Maria: an imaging and an ultrasound study, both read and released.
        var v4 = new Visit
        {
            VisitCode = $"VS-{Stamp()}-0004", PatientId = maria.Id, VisitDate = today.AddDays(-2),
            ReferringPhysician = "Dr. Lopez", Purpose = "Diagnostic imaging", Status = "Completed",
            TotalAmount = chestXray.Price + breastUs.Price, AmountPaid = chestXray.Price + breastUs.Price,
            PaymentStatus = "Paid", PaymentMethod = "Card"
        };
        db.Visits.Add(v4);
        db.SaveChanges();

        db.LabOrders.AddRange(
            new LabOrder
            {
                VisitId = v4.Id, LabTestId = chestXray.Id, Status = "Released",
                SpecimenBarcode = $"IMG-{Stamp()}-001",
                CollectedAt = today.AddDays(-2), ProcessedAt = today.AddDays(-2).AddHours(1),
                ReleasedAt = today.AddDays(-2).AddHours(2), ResultedBy = "Dr. Maria Lopez",
                NarrativeFindings =
                    "Lung fields are clear. Heart is not enlarged. Both hemidiaphragms and costophrenic "
                    + "sulci are intact. The visualised osseous structures are unremarkable.\n\n"
                    + "IMPRESSION: No significant chest findings."
            },
            new LabOrder
            {
                VisitId = v4.Id, LabTestId = breastUs.Id, Status = "Released",
                SpecimenBarcode = $"IMG-{Stamp()}-002",
                CollectedAt = today.AddDays(-2), ProcessedAt = today.AddDays(-2).AddHours(1),
                ReleasedAt = today.AddDays(-2).AddHours(3), ResultedBy = "Dr. Maria Lopez",
                IsAbnormal = true,
                NarrativeFindings =
                    "A well-circumscribed hypoechoic nodule measuring 0.8 x 0.6 cm is seen in the upper "
                    + "outer quadrant of the right breast. No posterior shadowing.\n\n"
                    + "IMPRESSION: Probably benign nodule, right breast. Follow-up in six months advised."
            }
        );

        // Juan: a tracing waiting to be read, so the For Reading tab is not empty.
        var v5 = new Visit
        {
            VisitCode = $"VS-{Stamp()}-0005", PatientId = juan.Id, VisitDate = today.AddHours(-2),
            ReferringPhysician = "Dr. Bautista", Purpose = "Cardiac screening", Status = "Processing",
            TotalAmount = ecg.Price, AmountPaid = 0, PaymentStatus = "Unpaid"
        };
        db.Visits.Add(v5);
        db.SaveChanges();

        db.LabOrders.Add(new LabOrder
        {
            VisitId = v5.Id, LabTestId = ecg.Id, Status = "Collected",
            SpecimenBarcode = $"HST-{Stamp()}-001", CollectedAt = today.AddHours(-1)
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Allergies, medications, history and vitals for one patient, so the
    /// patient chart is not an empty shell on a fresh database.
    /// </summary>
    private static void SeedChart(AppDbContext db, Patient patient, DateTime today)
    {
        if (db.PatientAllergies.Any()) return;

        db.PatientAllergies.AddRange(
            new PatientAllergy
            {
                PatientId = patient.Id, Substance = "Penicillin",
                Severity = "Severe", Reaction = "Urticaria and facial swelling"
            },
            new PatientAllergy
            {
                PatientId = patient.Id, Substance = "Shellfish",
                Severity = "Mild", Reaction = "Itching"
            }
        );

        db.PatientMedications.Add(new PatientMedication
        {
            PatientId = patient.Id, Name = "Folic Acid", Dosage = "5 mg",
            Frequency = "Once daily", Notes = "Preconception supplementation"
        });

        db.PatientConditions.Add(new PatientCondition
        {
            PatientId = patient.Id, Condition = "Polycystic ovary syndrome",
            DiagnosedOn = "2021", Notes = "Managed with lifestyle changes"
        });

        db.VitalSignRecords.AddRange(
            new VitalSignRecord
            {
                PatientId = patient.Id, RecordedAt = today.AddDays(-2), RecordedBy = "Dr. Maria Lopez",
                SystolicBp = 118, DiastolicBp = 76, HeartRate = 72, RespiratoryRate = 18,
                TemperatureC = 36.6m, WeightKg = 58.4m, HeightCm = 160m, OxygenSaturation = 98
            },
            new VitalSignRecord
            {
                PatientId = patient.Id, RecordedAt = today.AddHours(-5), RecordedBy = "Dr. Maria Lopez",
                SystolicBp = 120, DiastolicBp = 80, HeartRate = 66, RespiratoryRate = 20,
                TemperatureC = 36.8m, WeightKg = 58.1m, HeightCm = 160m, OxygenSaturation = 99
            }
        );

        db.SaveChanges();
    }

    /// <summary>
    /// Seeds a few sample cashier orders for a patient so the Patient Orders
    /// History table is populated out of the box. Mirrors the Ordered/Cancelled/Draft
    /// statuses shown in the cashier screenshots.
    /// </summary>
    private static void SeedOrders(AppDbContext db, Patient patient, DateTime today)
    {
        if (db.Orders.Any()) return;

        var products = db.Products.OrderBy(p => p.Id).ToList();
        if (products.Count == 0) return;

        string Stamp() => today.ToString("yyyyMMdd");

        Order MakeOrder(int seq, string status, DateTime createdAt, params (Product product, int qty)[] lines)
        {
            var order = new Order
            {
                OrderCode = $"OR-{Stamp()}-{seq:D4}",
                PatientId = patient.Id,
                Status = status,
                Referrer = "Dr. Tan",
                Branch = "Main Branch",
                ContactNumber = patient.ContactNumber,
                ContactEmail = patient.Email,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
            foreach (var (product, qty) in lines)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = qty,
                    UnitPrice = product.Price,
                    LineTotal = product.Price * qty
                });
            }
            order.SubTotal = order.Items.Sum(i => i.LineTotal);
            order.Total = order.SubTotal;
            return order;
        }

        var orders = new[]
        {
            MakeOrder(1, "Ordered",   today.AddDays(-3), (products[0], 1)),
            MakeOrder(2, "Cancelled", today.AddDays(-2), (products[1], 3)),
            MakeOrder(3, "Draft",     today.AddHours(-6), (products[2], 5))
        };
        db.Orders.AddRange(orders);
        db.SaveChanges();
    }
}
