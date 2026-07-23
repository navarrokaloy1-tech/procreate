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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                FullName = "System Administrator",
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
                Role = "Cashier",
                Email = "cashier@procreate.ai",
                IsActive = true
            });

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Code = "PRD-1001", Name = "Complete Blood Count", Category = "Laboratory", Price = 350, IsActive = true },
            new Product { Id = 2, Code = "PRD-1002", Name = "Transvaginal Ultrasound", Category = "Imaging", Price = 1500, IsActive = true },
            new Product { Id = 3, Code = "PRD-1003", Name = "Follicle Monitoring", Category = "Fertility", Price = 1200, IsActive = true },
            new Product { Id = 4, Code = "PRD-1004", Name = "Hormone Panel (FSH/LH/E2)", Category = "Laboratory", Price = 2800, IsActive = true },
            new Product { Id = 5, Code = "PRD-1005", Name = "Semen Analysis", Category = "Fertility", Price = 900, IsActive = true },
            new Product { Id = 6, Code = "PRD-1006", Name = "OB-GYN Consultation", Category = "Consultation", Price = 800, IsActive = true },
            new Product { Id = 7, Code = "PRD-1007", Name = "Pap Smear", Category = "Laboratory", Price = 650, IsActive = true },
            new Product { Id = 8, Code = "PRD-1008", Name = "Pelvic Ultrasound", Category = "Imaging", Price = 1300, IsActive = true }
        );

        modelBuilder.Entity<TestCategory>().HasData(
            new TestCategory { Id = 1, Name = "Hematology", Description = "Blood count and related tests" },
            new TestCategory { Id = 2, Name = "Clinical Chemistry", Description = "Blood chemistry panels" },
            new TestCategory { Id = 3, Name = "Urinalysis", Description = "Urine examination" },
            new TestCategory { Id = 4, Name = "Serology", Description = "Antibody and antigen tests" }
        );

        modelBuilder.Entity<LabTest>().HasData(
            new LabTest { Id = 1, CategoryId = 1, Code = "CBC", Name = "Complete Blood Count", Specimen = "Blood", Method = "Automated", Price = 350, TurnaroundHours = 2 },
            new LabTest { Id = 2, CategoryId = 2, Code = "FBS", Name = "Fasting Blood Sugar", Specimen = "Blood", Method = "Enzymatic", Price = 150, TurnaroundHours = 1 },
            new LabTest { Id = 3, CategoryId = 2, Code = "CREA", Name = "Creatinine", Specimen = "Blood", Method = "Jaffe", Price = 150, TurnaroundHours = 1 },
            new LabTest { Id = 4, CategoryId = 2, Code = "UA", Name = "Uric Acid", Specimen = "Blood", Method = "Enzymatic", Price = 150, TurnaroundHours = 1 },
            new LabTest { Id = 5, CategoryId = 3, Name = "Urinalysis", Code = "URA", Specimen = "Urine", Method = "Dipstick + Microscopy", Price = 120, TurnaroundHours = 1 },
            new LabTest { Id = 6, CategoryId = 4, Code = "HBsAg", Name = "Hepatitis B Surface Antigen", Specimen = "Blood", Method = "ELISA", Price = 450, TurnaroundHours = 4 }
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
