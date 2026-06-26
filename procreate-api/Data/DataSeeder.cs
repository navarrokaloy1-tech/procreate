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
    }
}
