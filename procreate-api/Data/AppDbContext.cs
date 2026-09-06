using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<TestCategory> TestCategories => Set<TestCategory>();
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<TestParameter> TestParameters => Set<TestParameter>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ResultDelivery> ResultDeliveries => Set<ResultDelivery>();
    public DbSet<AppointmentBatch> AppointmentBatches => Set<AppointmentBatch>();
    public DbSet<AppointmentBatchTemplate> AppointmentBatchTemplates => Set<AppointmentBatchTemplate>();
    public DbSet<MedicalCertificate> MedicalCertificates => Set<MedicalCertificate>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();
    public DbSet<PatientCondition> PatientConditions => Set<PatientCondition>();
    public DbSet<VitalSignRecord> VitalSignRecords => Set<VitalSignRecord>();
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<ServiceInventoryItem> ServiceInventoryItems => Set<ServiceInventoryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                FullName = "System Administrator",
                FirstName = "System", LastName = "Administrator",
                Role = "Admin",
                Email = "admin@procreate.ai",
                IsActive = true
            },
            new User
            {
                Id = 2,
                Username = "cashier",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("cashier123"),
                FullName = "John Doe",
                FirstName = "John", LastName = "Doe",
                Role = "Cashier",
                Email = "cashier@procreate.ai",
                IsActive = true
            },
            new User
            {
                Id = 3,
                Username = "doctor",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("doctor123"),
                FullName = "Dr. Maria Lopez",
                FirstName = "Maria", LastName = "Lopez",
                Role = "Doctor",
                Email = "mlopez@procreate.ai",
                IsActive = true
            });

        // Doctor 1 is linked to the seeded Doctor login so the doctor-scoped
        // views can be exercised; doctor 2 has no system access.
        modelBuilder.Entity<Doctor>().HasData(
            new Doctor
            {
                Id = 1, DoctorCode = "DR-0001", UserId = 3,
                FirstName = "Maria", LastName = "Lopez", Gender = "Female",
                Specialty = "Obstetrics and Gynecology", SubSpecialty = "Reproductive Endocrinology",
                Email = "mlopez@procreate.ai", ContactNumber = "09171112222",
                PrcLicenseNumber = "0123456", ConsultationFee = 800, FollowUpFee = 500,
                SpecialistFee = 1200, IsActive = true
            },
            new Doctor
            {
                Id = 2, DoctorCode = "DR-0002",
                FirstName = "Ramon", LastName = "Bautista", Gender = "Male",
                Specialty = "Obstetrics and Gynecology",
                Email = "rbautista@procreate.ai", ContactNumber = "09173334444",
                PrcLicenseNumber = "0654321", ConsultationFee = 700, FollowUpFee = 450,
                SpecialistFee = 1000, IsActive = true
            }
        );

        modelBuilder.Entity<DoctorSchedule>().HasData(
            Enumerable.Range(0, 7).SelectMany(day => new[]
            {
                new DoctorSchedule
                {
                    Id = 1 + day, DoctorId = 1, DayOfWeek = day,
                    IsAvailable = day >= 1 && day <= 5, StartTime = "08:00", EndTime = "17:00"
                },
                new DoctorSchedule
                {
                    Id = 8 + day, DoctorId = 2, DayOfWeek = day,
                    IsAvailable = day >= 1 && day <= 6, StartTime = "09:00", EndTime = "16:00"
                }
            }).ToArray()
        );

        // Three session blocks a day, five patients each. Sunday is defined but
        // inactive, so opening it is a matter of flipping a flag rather than
        // inventing the blocks.
        modelBuilder.Entity<AppointmentBatchTemplate>().HasData(
            Enumerable.Range(0, 7).SelectMany(day => new[]
            {
                new AppointmentBatchTemplate
                {
                    Id = 1 + (day * 3), DayOfWeek = day,
                    StartTime = "09:00", EndTime = "12:00", Capacity = 5, IsActive = day != 0
                },
                new AppointmentBatchTemplate
                {
                    Id = 2 + (day * 3), DayOfWeek = day,
                    StartTime = "12:00", EndTime = "15:00", Capacity = 5, IsActive = day != 0
                },
                new AppointmentBatchTemplate
                {
                    Id = 3 + (day * 3), DayOfWeek = day,
                    StartTime = "15:00", EndTime = "18:00", Capacity = 5, IsActive = day != 0
                }
            }).ToArray()
        );

        // A doctor's login may be cleared without deleting the practitioner record.
        modelBuilder.Entity<Doctor>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // A queue ticket can be anonymous (walk-in enquiry) or tied to a patient.
        modelBuilder.Entity<QueueEntry>()
            .HasOne(q => q.Patient)
            .WithMany()
            .HasForeignKey(q => q.PatientId)
            .OnDelete(DeleteBehavior.SetNull);

        // Queue numbers are allocated per day, so the pair must stay unique.
        modelBuilder.Entity<QueueEntry>()
            .HasIndex(q => new { q.QueueDate, q.QueueNumber })
            .IsUnique();

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Doctor)
            .WithMany()
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Appointment>()
            .HasIndex(a => a.ScheduledAt);

        // Deleting a block releases its appointments rather than taking them
        // with it — a booking is the patient's, not the block's.
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Batch)
            .WithMany(b => b.Appointments)
            .HasForeignKey(a => a.BatchId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Appointment>()
            .HasIndex(a => new { a.BatchId, a.QueuePosition });

        // One block per start time per day. Days materialise their blocks from
        // the template on first open, and two simultaneous opens would
        // otherwise each insert a set.
        // The audit trail outlives nothing: a delivery row is meaningless
        // without the patient it was for, so it goes when they do.
        modelBuilder.Entity<ResultDelivery>()
            .HasOne(d => d.Patient)
            .WithMany()
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ResultDelivery>()
            .HasOne(d => d.Doctor)
            .WithMany()
            .HasForeignKey(d => d.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ResultDelivery>()
            .HasIndex(d => new { d.PatientId, d.CreatedAt });

        modelBuilder.Entity<AppointmentBatch>()
            .HasIndex(b => new { b.BatchDate, b.StartTime })
            .IsUnique();

        modelBuilder.Entity<AppointmentBatchTemplate>()
            .HasIndex(t => new { t.DayOfWeek, t.StartTime })
            .IsUnique();

        // A certificate outlives the patient record it was issued against, and
        // walk-in certificates have no patient at all.
        modelBuilder.Entity<MedicalCertificate>()
            .HasOne(c => c.Patient)
            .WithMany()
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MedicalCertificate>()
            .HasOne(c => c.Doctor)
            .WithMany()
            .HasForeignKey(c => c.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MedicalCertificate>()
            .HasIndex(c => c.CertificateNumber)
            .IsUnique();

        // The chart belongs to the patient: deleting the patient takes the whole
        // clinical record with it. Each side is indexed by PatientId because
        // every read is "everything for this patient".
        foreach (var chartEntity in new[]
                 {
                     typeof(PatientAllergy), typeof(PatientMedication),
                     typeof(PatientCondition), typeof(VitalSignRecord),
                     typeof(PatientDocument)
                 })
        {
            modelBuilder.Entity(chartEntity)
                .HasOne("Patient")
                .WithMany()
                .HasForeignKey("PatientId")
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity(chartEntity).HasIndex("PatientId");
        }

        // Vitals and documents are always shown newest-first.
        modelBuilder.Entity<VitalSignRecord>().HasIndex(v => v.RecordedAt);
        modelBuilder.Entity<PatientDocument>().HasIndex(d => d.ResultDate);

        // An item keeps its history when its category or supplier is removed,
        // so both links go null rather than cascading.
        modelBuilder.Entity<InventoryItem>()
            .HasOne(i => i.Category)
            .WithMany()
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InventoryItem>()
            .HasOne(i => i.Supplier)
            .WithMany()
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        // The link row is meaningless without either end, so it goes with them.
        modelBuilder.Entity<ServiceInventoryItem>()
            .HasOne(l => l.Product)
            .WithMany(p => p.RequiredItems)
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceInventoryItem>()
            .HasOne(l => l.InventoryItem)
            .WithMany()
            .HasForeignKey(l => l.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // A service lists each reagent once.
        modelBuilder.Entity<ServiceInventoryItem>()
            .HasIndex(l => new { l.ProductId, l.InventoryItemId })
            .IsUnique();

        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Code = "LAB-001", Name = "Complete Blood Count", Category = "Laboratory", Price = 350, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 2, Code = "US-001", Name = "Transvaginal Ultrasound", Category = "Ultrasound", Price = 1500, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 3, Code = "US-002", Name = "Follicle Monitoring", Category = "Ultrasound", Price = 1200, IsActive = true, SeniorPwdDiscount = true },
            new Product { Id = 4, Code = "LAB-002", Name = "Hormone Panel (FSH/LH/E2)", Category = "Laboratory", Price = 2800, IsActive = true, SeniorPwdDiscount = true },
            new Product { Id = 5, Code = "LAB-003", Name = "Semen Analysis", Category = "Laboratory", Price = 900, IsActive = true, SeniorPwdDiscount = true },
            new Product { Id = 6, Code = "CON-001", Name = "OB-GYN Consultation", Category = "Consultation", Price = 800, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 7, Code = "LAB-004", Name = "Pap Smear", Category = "Laboratory", Price = 650, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 8, Code = "US-003", Name = "Pelvic Ultrasound", Category = "Ultrasound", Price = 1300, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 9, Code = "XR-001", Name = "Chest X-Ray (PA)", Category = "Imaging", Price = 400, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 10, Code = "XR-002", Name = "Chest X-Ray (APL)", Category = "Imaging", Price = 600, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 11, Code = "HS-001", Name = "12-Lead ECG", Category = "Heart Station", Price = 450, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 12, Code = "HS-002", Name = "2D Echocardiogram", Category = "Heart Station", Price = 3500, IsActive = true, SeniorPwdDiscount = true },
            new Product { Id = 13, Code = "CON-002", Name = "Follow-up Consultation", Category = "Consultation", Price = 350, IsActive = true, SeniorPwdDiscount = true, PhilHealthCovered = true },
            new Product { Id = 14, Code = "MISC-001", Name = "Fit to Work Certificate", Category = "Others", Price = 250, IsActive = true, SeniorPwdDiscount = true },
            new Product { Id = 15, Code = "MISC-002", Name = "Drug Test (5 Panel)", Category = "Laboratory", Price = 500, IsActive = true, SeniorPwdDiscount = true },
            new Product { Id = 16, Code = "US-004", Name = "Breast Ultrasound", Category = "Ultrasound", Price = 1200, IsActive = true }
        );

        modelBuilder.Entity<InventoryCategory>().HasData(
            new InventoryCategory { Id = 1, Name = "Ultrasound Supplies", Description = "Gels, probe covers and paper" },
            new InventoryCategory { Id = 2, Name = "Phlebotomy", Description = "Blood collection consumables" },
            new InventoryCategory { Id = 3, Name = "Pharmacy", Description = "Dispensed medicines" }
        );

        modelBuilder.Entity<Supplier>().HasData(
            new Supplier { Id = 1, Name = "MedGrocer Supply Co.", ContactPerson = "Ana Villanueva", ContactNumber = "09171230000", Email = "sales@medgrocer.example", IsActive = true },
            new Supplier { Id = 2, Name = "Zuellig Pharma", ContactPerson = "Mark Tan", ContactNumber = "09281234444", Email = "orders@zuellig.example", IsActive = true }
        );

        // A spread of stock states so the dashboard tiles and the low-stock /
        // out-of-stock alerts have something real to count on a fresh database.
        modelBuilder.Entity<InventoryItem>().HasData(
            new InventoryItem { Id = 1, ItemType = ItemTypes.Consumable, CategoryId = 1, SupplierId = 1, Name = "Ultrasound Gel", BrandName = "Aquasonic", UnitOfMeasure = "Bottle", Sku = "US-GEL-01", CostPrice = 180, SellingPrice = 250, CurrentStock = 24, ReorderLevel = 10, MinOrderQty = 6 },
            new InventoryItem { Id = 2, ItemType = ItemTypes.Consumable, CategoryId = 1, SupplierId = 1, Name = "Probe Cover", BrandName = "Fit One", UnitOfMeasure = "Piece", Sku = "US-PC-01", CostPrice = 5, SellingPrice = 12, CurrentStock = 139, ReorderLevel = 50, MinOrderQty = 100 },
            new InventoryItem { Id = 3, ItemType = ItemTypes.Consumable, CategoryId = 2, SupplierId = 1, Name = "Cotton Balls", BrandName = "Indoplas", UnitOfMeasure = "Pack", Sku = "PH-CB-01", CostPrice = 25, SellingPrice = 40, CurrentStock = 99, ReorderLevel = 20, MinOrderQty = 10 },
            new InventoryItem { Id = 4, ItemType = ItemTypes.Consumable, CategoryId = 2, SupplierId = 1, Name = "Vacutainer Tube (EDTA)", BrandName = "BD", UnitOfMeasure = "Box", SubUnit = "Piece", ConversionFactor = 100, Sku = "PH-VT-01", CostPrice = 850, SellingPrice = 1100, CurrentStock = 6, ReorderLevel = 8, MinOrderQty = 2 },
            new InventoryItem { Id = 5, ItemType = ItemTypes.Medicine, CategoryId = 3, SupplierId = 2, Name = "Paracetamol", BrandName = "Biogesic", Dosage = "500mg", UnitOfMeasure = "Box", SubUnit = "Tablet", ConversionFactor = 100, Sku = "MED-001", CostPrice = 150, SellingPrice = 300, CurrentStock = 0, ReorderLevel = 5, MinOrderQty = 2 },
            new InventoryItem { Id = 6, ItemType = ItemTypes.Medicine, CategoryId = 3, SupplierId = 2, Name = "Mefenamic Acid", BrandName = "Dolfenal", Dosage = "500mg", UnitOfMeasure = "Box", SubUnit = "Capsule", ConversionFactor = 50, Sku = "MED-002", CostPrice = 140, SellingPrice = 240, CurrentStock = 0, ReorderLevel = 5, MinOrderQty = 2 },
            new InventoryItem { Id = 7, ItemType = ItemTypes.Medicine, CategoryId = 3, SupplierId = 2, Name = "Amlodipine", BrandName = "Norvasc", Dosage = "50mg", UnitOfMeasure = "Box", SubUnit = "Tablet", ConversionFactor = 30, Sku = "MED-003", CostPrice = 150, SellingPrice = 300, CurrentStock = 40, ReorderLevel = 10, MinOrderQty = 5 },
            new InventoryItem { Id = 8, ItemType = ItemTypes.Medicine, CategoryId = 3, SupplierId = 2, Name = "Losartan", BrandName = "Sartan", Dosage = "50mg", UnitOfMeasure = "Box", SubUnit = "Tablet", ConversionFactor = 30, Sku = "MED-004", CostPrice = 150, SellingPrice = 300, CurrentStock = 4, ReorderLevel = 10, MinOrderQty = 5 },
            new InventoryItem { Id = 9, ItemType = ItemTypes.Asset, SupplierId = 1, Name = "Stethoscope", BrandName = "Littmann", UnitOfMeasure = "Piece", Sku = "AST-001", CostPrice = 5000, SellingPrice = 0, CurrentStock = 3, ReorderLevel = 1, MinOrderQty = 1 },
            new InventoryItem { Id = 10, ItemType = ItemTypes.Asset, SupplierId = 1, Name = "Digital BP Monitor", BrandName = "Omron", UnitOfMeasure = "Piece", Sku = "AST-002", CostPrice = 3200, SellingPrice = 0, CurrentStock = 2, ReorderLevel = 1, MinOrderQty = 1 }
        );

        // Two services that actually draw stock, so the link is exercised.
        modelBuilder.Entity<ServiceInventoryItem>().HasData(
            new ServiceInventoryItem { Id = 1, ProductId = 2, InventoryItemId = 1, Quantity = 1 },
            new ServiceInventoryItem { Id = 2, ProductId = 2, InventoryItemId = 2, Quantity = 1 },
            new ServiceInventoryItem { Id = 3, ProductId = 1, InventoryItemId = 3, Quantity = 1 },
            new ServiceInventoryItem { Id = 4, ProductId = 1, InventoryItemId = 4, Quantity = 1 }
        );

        // Categories carry the department, so a test's console tab follows from
        // the category it sits in. Categories 5-7 have no test parameters:
        // those studies are read and written up, not measured.
        modelBuilder.Entity<TestCategory>().HasData(
            new TestCategory { Id = 1, Name = "Hematology", Description = "Blood count and related tests", Department = Departments.Laboratory },
            new TestCategory { Id = 2, Name = "Clinical Chemistry", Description = "Blood chemistry panels", Department = Departments.Laboratory },
            new TestCategory { Id = 3, Name = "Urinalysis", Description = "Urine examination", Department = Departments.Laboratory },
            new TestCategory { Id = 4, Name = "Serology", Description = "Antibody and antigen tests", Department = Departments.Laboratory },
            new TestCategory { Id = 5, Name = "Radiology", Description = "Plain-film radiography", Department = Departments.Imaging },
            new TestCategory { Id = 6, Name = "Sonography", Description = "Ultrasound studies", Department = Departments.Ultrasound },
            new TestCategory { Id = 7, Name = "Cardiology", Description = "Cardiac tracing and echo", Department = Departments.HeartStation }
        );

        modelBuilder.Entity<LabTest>().HasData(
            new LabTest { Id = 1, CategoryId = 1, Code = "CBC", Name = "Complete Blood Count", Specimen = "Blood", Method = "Automated", Price = 350, TurnaroundHours = 2 },
            new LabTest { Id = 2, CategoryId = 2, Code = "FBS", Name = "Fasting Blood Sugar", Specimen = "Blood", Method = "Enzymatic", Price = 150, TurnaroundHours = 1 },
            new LabTest { Id = 3, CategoryId = 2, Code = "CREA", Name = "Creatinine", Specimen = "Blood", Method = "Jaffe", Price = 150, TurnaroundHours = 1 },
            new LabTest { Id = 4, CategoryId = 2, Code = "UA", Name = "Uric Acid", Specimen = "Blood", Method = "Enzymatic", Price = 150, TurnaroundHours = 1 },
            new LabTest { Id = 5, CategoryId = 3, Name = "Urinalysis", Code = "URA", Specimen = "Urine", Method = "Dipstick + Microscopy", Price = 120, TurnaroundHours = 1 },
            new LabTest { Id = 6, CategoryId = 4, Code = "HBsAg", Name = "Hepatitis B Surface Antigen", Specimen = "Blood", Method = "ELISA", Price = 450, TurnaroundHours = 4 },

            // Imaging
            new LabTest { Id = 7, CategoryId = 5, Code = "CXR-PA", Name = "Chest X-Ray (PA)", Specimen = "None", Method = "Digital Radiography", Price = 500, TurnaroundHours = 2 },
            new LabTest { Id = 8, CategoryId = 5, Code = "CXR-APL", Name = "Chest X-Ray (APL)", Specimen = "None", Method = "Digital Radiography", Price = 650, TurnaroundHours = 2 },
            new LabTest { Id = 9, CategoryId = 5, Code = "PXR", Name = "Pelvic X-Ray", Specimen = "None", Method = "Digital Radiography", Price = 600, TurnaroundHours = 2 },

            // Ultrasound
            new LabTest { Id = 10, CategoryId = 6, Code = "TVS", Name = "Transvaginal Ultrasound", Specimen = "None", Method = "Sonography", Price = 1500, TurnaroundHours = 2 },
            new LabTest { Id = 11, CategoryId = 6, Code = "PUS", Name = "Pelvic Ultrasound", Specimen = "None", Method = "Sonography", Price = 1300, TurnaroundHours = 2 },
            new LabTest { Id = 12, CategoryId = 6, Code = "BUS", Name = "Breast Ultrasound", Specimen = "None", Method = "Sonography", Price = 1400, TurnaroundHours = 2 },
            new LabTest { Id = 13, CategoryId = 6, Code = "FOLM", Name = "Follicle Monitoring", Specimen = "None", Method = "Sonography", Price = 1200, TurnaroundHours = 1 },

            // Heart Station
            new LabTest { Id = 14, CategoryId = 7, Code = "ECG", Name = "12-Lead ECG", Specimen = "None", Method = "Electrocardiography", Price = 450, TurnaroundHours = 1 },
            new LabTest { Id = 15, CategoryId = 7, Code = "2DECHO", Name = "2D Echocardiogram", Specimen = "None", Method = "Doppler Echocardiography", Price = 3500, TurnaroundHours = 4 }
        );

        modelBuilder.Entity<TestParameter>().HasData(
            new TestParameter { Id = 1, LabTestId = 1, Name = "WBC", Unit = "10^9/L", ReferenceRange = "4.5-11.0", NormalMin = "4.5", NormalMax = "11.0" },
            new TestParameter { Id = 2, LabTestId = 1, Name = "RBC", Unit = "10^12/L", ReferenceRange = "4.5-5.5", NormalMin = "4.5", NormalMax = "5.5" },
            new TestParameter { Id = 3, LabTestId = 1, Name = "Hemoglobin", Unit = "g/dL", ReferenceRange = "13.5-17.5", NormalMin = "13.5", NormalMax = "17.5" },
            new TestParameter { Id = 4, LabTestId = 1, Name = "Hematocrit", Unit = "%", ReferenceRange = "41-53", NormalMin = "41", NormalMax = "53" },
            new TestParameter { Id = 5, LabTestId = 1, Name = "Platelets", Unit = "10^9/L", ReferenceRange = "150-400", NormalMin = "150", NormalMax = "400" },
            new TestParameter { Id = 6, LabTestId = 2, Name = "Glucose", Unit = "mg/dL", ReferenceRange = "70-100", NormalMin = "70", NormalMax = "100" },
            new TestParameter { Id = 7, LabTestId = 3, Name = "Creatinine", Unit = "mg/dL", ReferenceRange = "0.7-1.3", NormalMin = "0.7", NormalMax = "1.3" },
            new TestParameter { Id = 8, LabTestId = 4, Name = "Uric Acid", Unit = "mg/dL", ReferenceRange = "3.4-7.0", NormalMin = "3.4", NormalMax = "7.0" }
        );
    }
}
