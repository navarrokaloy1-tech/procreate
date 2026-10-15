using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace procreate_api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartTime = table.Column<string>(type: "text", nullable: false),
                    EndTime = table.Column<string>(type: "text", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentBatchTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<string>(type: "text", nullable: false),
                    EndTime = table.Column<string>(type: "text", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentBatchTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Features = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsernameAttempted = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Audience = table.Column<string>(type: "text", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientCode = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    ContactNumber = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    BloodType = table.Column<string>(type: "text", nullable: false),
                    EmergencyContactName = table.Column<string>(type: "text", nullable: false),
                    EmergencyContactNumber = table.Column<string>(type: "text", nullable: false),
                    CivilStatus = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    Occupation = table.Column<string>(type: "text", nullable: true),
                    Suffix = table.Column<string>(type: "text", nullable: true),
                    Landline = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Region = table.Column<string>(type: "text", nullable: true),
                    Province = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    ZipCode = table.Column<string>(type: "text", nullable: true),
                    EmergencyContactRelationship = table.Column<string>(type: "text", nullable: true),
                    EmergencyContactNotes = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    PhilHealthNumber = table.Column<string>(type: "text", nullable: true),
                    SeniorCitizenId = table.Column<string>(type: "text", nullable: true),
                    PwdId = table.Column<string>(type: "text", nullable: true),
                    HmoProvider = table.Column<string>(type: "text", nullable: true),
                    HmoAccountNumber = table.Column<string>(type: "text", nullable: true),
                    ReferralSource = table.Column<string>(type: "text", nullable: true),
                    ReferralDetail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LisRegisteredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CardToken = table.Column<string>(type: "text", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PortalEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SeniorPwdDiscount = table.Column<bool>(type: "boolean", nullable: false),
                    PhilHealthCovered = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ContactPerson = table.Column<string>(type: "text", nullable: false),
                    ContactNumber = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestPanels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestPanels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Suffix = table.Column<string>(type: "text", nullable: false),
                    Sex = table.Column<string>(type: "text", nullable: false),
                    Birthday = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ContactNumber = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomRoleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_CustomRoles_CustomRoleId",
                        column: x => x.CustomRoleId,
                        principalTable: "CustomRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderCode = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Referrer = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    Branch = table.Column<string>(type: "text", nullable: false),
                    ContactNumber = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    Total = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientAllergies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Substance = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Reaction = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAllergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAllergies_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    DiagnosedOn = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientConditions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ResultDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientDocuments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientMedications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Dosage = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientMedications_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QueueEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QueueNumber = table.Column<int>(type: "integer", nullable: false),
                    QueueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PatientName = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CalledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueEntries_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitCode = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReferringPhysician = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VitalSignRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    SystolicBp = table.Column<int>(type: "integer", nullable: true),
                    DiastolicBp = table.Column<int>(type: "integer", nullable: true),
                    HeartRate = table.Column<int>(type: "integer", nullable: true),
                    RespiratoryRate = table.Column<int>(type: "integer", nullable: true),
                    TemperatureC = table.Column<decimal>(type: "numeric", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric", nullable: true),
                    OxygenSaturation = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalSignRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VitalSignRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemType = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    BrandName = table.Column<string>(type: "text", nullable: false),
                    Dosage = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "text", nullable: false),
                    SubUnit = table.Column<string>(type: "text", nullable: false),
                    ConversionFactor = table.Column<int>(type: "integer", nullable: true),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentStock = table.Column<int>(type: "integer", nullable: false),
                    ReorderLevel = table.Column<int>(type: "integer", nullable: false),
                    MinOrderQty = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_InventoryCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "InventoryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LabTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Specimen = table.Column<string>(type: "text", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    TurnaroundHours = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabTests_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabTests_TestCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TestCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoctorCode = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Suffix = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    Specialty = table.Column<string>(type: "text", nullable: false),
                    SubSpecialty = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    ContactNumber = table.Column<string>(type: "text", nullable: false),
                    PrcLicenseNumber = table.Column<string>(type: "text", nullable: false),
                    PrcLicenseExpiry = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PtrNumber = table.Column<string>(type: "text", nullable: false),
                    S2LicenseNumber = table.Column<string>(type: "text", nullable: false),
                    TinNumber = table.Column<string>(type: "text", nullable: false),
                    ConsultationFee = table.Column<decimal>(type: "numeric", nullable: false),
                    FollowUpFee = table.Column<decimal>(type: "numeric", nullable: false),
                    SpecialistFee = table.Column<decimal>(type: "numeric", nullable: false),
                    Bio = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SignatureImage = table.Column<byte[]>(type: "bytea", nullable: true),
                    SignatureContentType = table.Column<string>(type: "text", nullable: false),
                    SignatureFileName = table.Column<string>(type: "text", nullable: false),
                    SignatureUploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Doctors_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BillNumber = table.Column<string>(type: "text", nullable: false),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric", nullable: false),
                    Total = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric", nullable: false),
                    Change = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bills_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceInventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceInventoryItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceInventoryItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    QuantityReceived = table.Column<int>(type: "integer", nullable: false),
                    QuantityRemaining = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiryNoticeStage = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockBatches_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SpecimenBarcode = table.Column<string>(type: "text", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NarrativeFindings = table.Column<string>(type: "text", nullable: false),
                    IsAbnormal = table.Column<bool>(type: "boolean", nullable: false),
                    ResultedBy = table.Column<string>(type: "text", nullable: false),
                    LisStatus = table.Column<string>(type: "text", nullable: true),
                    LisOrderId = table.Column<string>(type: "text", nullable: true),
                    LisSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LisAckedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LisError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrders_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabOrders_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestInventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestInventoryItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestInventoryItems_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestPanelItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PanelId = table.Column<int>(type: "integer", nullable: false),
                    LabTestId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestPanelItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestPanelItems_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestPanelItems_TestPanels_PanelId",
                        column: x => x.PanelId,
                        principalTable: "TestPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    ReferenceRange = table.Column<string>(type: "text", nullable: false),
                    NormalMin = table.Column<string>(type: "text", nullable: true),
                    NormalMax = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestParameters_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentCode = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    Service = table.Column<string>(type: "text", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ChiefComplaint = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    BatchId = table.Column<int>(type: "integer", nullable: true),
                    QueuePosition = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_AppointmentBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AppointmentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Appointments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    StartTime = table.Column<string>(type: "text", nullable: false),
                    EndTime = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSchedules_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MedicalCertificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificateNumber = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    WalkInName = table.Column<string>(type: "text", nullable: false),
                    WalkInAge = table.Column<string>(type: "text", nullable: false),
                    WalkInAddress = table.Column<string>(type: "text", nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Template = table.Column<string>(type: "text", nullable: false),
                    Diagnosis = table.Column<string>(type: "text", nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalCertificates_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalCertificates_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ResultDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    SentTo = table.Column<string>(type: "text", nullable: false),
                    LabOrderIds = table.Column<string>(type: "text", nullable: false),
                    DocumentIds = table.Column<string>(type: "text", nullable: false),
                    IncludeSignature = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: false),
                    SentBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultDeliveries_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultDeliveries_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    MovementType = table.Column<string>(type: "text", nullable: false),
                    QuantityChange = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    PerformedBy = table.Column<string>(type: "text", nullable: false),
                    BatchId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LabResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabOrderId = table.Column<int>(type: "integer", nullable: false),
                    TestParameterId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Flag = table.Column<string>(type: "text", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResults_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabResults_TestParameters_TestParameterId",
                        column: x => x.TestParameterId,
                        principalTable: "TestParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppointmentBatchTemplates",
                columns: new[] { "Id", "Capacity", "DayOfWeek", "EndTime", "IsActive", "StartTime" },
                values: new object[,]
                {
                    { 1, 5, 0, "12:00", false, "09:00" },
                    { 2, 5, 0, "15:00", false, "12:00" },
                    { 3, 5, 0, "18:00", false, "15:00" },
                    { 4, 5, 1, "12:00", true, "09:00" },
                    { 5, 5, 1, "15:00", true, "12:00" },
                    { 6, 5, 1, "18:00", true, "15:00" },
                    { 7, 5, 2, "12:00", true, "09:00" },
                    { 8, 5, 2, "15:00", true, "12:00" },
                    { 9, 5, 2, "18:00", true, "15:00" },
                    { 10, 5, 3, "12:00", true, "09:00" },
                    { 11, 5, 3, "15:00", true, "12:00" },
                    { 12, 5, 3, "18:00", true, "15:00" },
                    { 13, 5, 4, "12:00", true, "09:00" },
                    { 14, 5, 4, "15:00", true, "12:00" },
                    { 15, 5, 4, "18:00", true, "15:00" },
                    { 16, 5, 5, "12:00", true, "09:00" },
                    { 17, 5, 5, "15:00", true, "12:00" },
                    { 18, 5, 5, "18:00", true, "15:00" },
                    { 19, 5, 6, "12:00", true, "09:00" },
                    { 20, 5, 6, "15:00", true, "12:00" },
                    { 21, 5, 6, "18:00", true, "15:00" }
                });

            migrationBuilder.InsertData(
                table: "Doctors",
                columns: new[] { "Id", "Bio", "ConsultationFee", "ContactNumber", "CreatedAt", "DoctorCode", "Email", "FirstName", "FollowUpFee", "Gender", "IsActive", "LastName", "MiddleName", "PrcLicenseExpiry", "PrcLicenseNumber", "PtrNumber", "S2LicenseNumber", "SignatureContentType", "SignatureFileName", "SignatureImage", "SignatureUploadedAt", "SpecialistFee", "Specialty", "SubSpecialty", "Suffix", "TinNumber", "UserId" },
                values: new object[] { 2, "", 700m, "09173334444", new DateTime(2026, 10, 15, 3, 28, 6, 698, DateTimeKind.Utc).AddTicks(7710), "DR-0002", "rbautista@procreate.ai", "Ramon", 450m, "Male", true, "Bautista", "", null, "0654321", "", "", "", "", null, null, 1000m, "Obstetrics and Gynecology", "", "", "", null });

            migrationBuilder.InsertData(
                table: "InventoryCategories",
                columns: new[] { "Id", "CreatedAt", "Description", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 15, 3, 28, 6, 754, DateTimeKind.Utc).AddTicks(8231), "Gels, probe covers and paper", "Ultrasound Supplies" },
                    { 2, new DateTime(2026, 10, 15, 3, 28, 6, 755, DateTimeKind.Utc).AddTicks(549), "Blood collection consumables", "Phlebotomy" },
                    { 3, new DateTime(2026, 10, 15, 3, 28, 6, 755, DateTimeKind.Utc).AddTicks(553), "Dispensed medicines", "Pharmacy" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Category", "Code", "Description", "IsActive", "Name", "PhilHealthCovered", "Price", "SeniorPwdDiscount" },
                values: new object[,]
                {
                    { 1, "Laboratory", "LAB-001", "", true, "Complete Blood Count", true, 350m, true },
                    { 2, "Ultrasound", "US-001", "", true, "Transvaginal Ultrasound", true, 1500m, true },
                    { 3, "Ultrasound", "US-002", "", true, "Follicle Monitoring", false, 1200m, true },
                    { 4, "Laboratory", "LAB-002", "", true, "Hormone Panel (FSH/LH/E2)", false, 2800m, true },
                    { 5, "Laboratory", "LAB-003", "", true, "Semen Analysis", false, 900m, true },
                    { 6, "Consultation", "CON-001", "", true, "OB-GYN Consultation", true, 800m, true },
                    { 7, "Laboratory", "LAB-004", "", true, "Pap Smear", true, 650m, true },
                    { 8, "Ultrasound", "US-003", "", true, "Pelvic Ultrasound", true, 1300m, true },
                    { 9, "Imaging", "XR-001", "", true, "Chest X-Ray (PA)", true, 400m, true },
                    { 10, "Imaging", "XR-002", "", true, "Chest X-Ray (APL)", true, 600m, true },
                    { 11, "Heart Station", "HS-001", "", true, "12-Lead ECG", true, 450m, true },
                    { 12, "Heart Station", "HS-002", "", true, "2D Echocardiogram", false, 3500m, true },
                    { 13, "Consultation", "CON-002", "", true, "Follow-up Consultation", true, 350m, true },
                    { 14, "Others", "MISC-001", "", true, "Fit to Work Certificate", false, 250m, true },
                    { 15, "Laboratory", "MISC-002", "", true, "Drug Test (5 Panel)", false, 500m, true },
                    { 16, "Ultrasound", "US-004", "", true, "Breast Ultrasound", false, 1200m, false }
                });

            migrationBuilder.InsertData(
                table: "Suppliers",
                columns: new[] { "Id", "Address", "ContactNumber", "ContactPerson", "CreatedAt", "Email", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "", "09171230000", "Ana Villanueva", new DateTime(2026, 10, 15, 3, 28, 6, 755, DateTimeKind.Utc).AddTicks(2827), "sales@medgrocer.example", true, "MedGrocer Supply Co." },
                    { 2, "", "09281234444", "Mark Tan", new DateTime(2026, 10, 15, 3, 28, 6, 755, DateTimeKind.Utc).AddTicks(7164), "orders@zuellig.example", true, "Zuellig Pharma" }
                });

            migrationBuilder.InsertData(
                table: "TestCategories",
                columns: new[] { "Id", "Department", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Laboratory", "Blood count and related tests", "Hematology" },
                    { 2, "Laboratory", "Blood chemistry panels", "Clinical Chemistry" },
                    { 3, "Laboratory", "Urine examination", "Urinalysis" },
                    { 4, "Laboratory", "Antibody and antigen tests", "Serology" },
                    { 5, "Imaging", "Plain-film radiography", "Radiology" },
                    { 6, "Ultrasound", "Ultrasound studies", "Sonography" },
                    { 7, "Heart Station", "Cardiac tracing and echo", "Cardiology" }
                });

            migrationBuilder.InsertData(
                table: "TestPanels",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "APE", new DateTime(2026, 10, 15, 3, 28, 6, 766, DateTimeKind.Utc).AddTicks(3946), "Routine yearly check-up battery", true, "Annual Physical Exam" },
                    { 2, "PREEMP", new DateTime(2026, 10, 15, 3, 28, 6, 766, DateTimeKind.Utc).AddTicks(9767), "Standard pre-employment screening", true, "Pre-Employment" },
                    { 3, "CHEM", new DateTime(2026, 10, 15, 3, 28, 6, 766, DateTimeKind.Utc).AddTicks(9796), "Fasting blood sugar, creatinine and uric acid", true, "Basic Chemistry" },
                    { 4, "HEMA", new DateTime(2026, 10, 15, 3, 28, 6, 766, DateTimeKind.Utc).AddTicks(9803), "Complete blood count", true, "Hematology" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Address", "Birthday", "ContactNumber", "CreatedAt", "CustomRoleId", "Email", "FirstName", "FullName", "IsActive", "LastLoginAt", "LastName", "MiddleName", "MustChangePassword", "PasswordHash", "Role", "Sex", "Suffix", "Username" },
                values: new object[,]
                {
                    { 1, "", null, "", new DateTime(2026, 10, 15, 3, 28, 5, 700, DateTimeKind.Utc).AddTicks(4739), null, "admin@procreate.ai", "System", "System Administrator", true, null, "Administrator", "", false, "$2a$11$3SanJi6vnrDbVFfJXFDkK.HTte9xgVBGoGPG8UzFpyqL0dj/2KSeG", "Admin", "", "", "admin" },
                    { 2, "", null, "", new DateTime(2026, 10, 15, 3, 28, 6, 142, DateTimeKind.Utc).AddTicks(8683), null, "cashier@procreate.ai", "John", "John Doe", true, null, "Doe", "", false, "$2a$11$s7rMrqDi2tmSkZlE5hIYf.jH8PXYSCJkfttpggd85WMHy0bH.NKZq", "Cashier", "", "", "cashier" },
                    { 3, "", null, "", new DateTime(2026, 10, 15, 3, 28, 6, 429, DateTimeKind.Utc).AddTicks(6919), null, "mlopez@procreate.ai", "Maria", "Dr. Maria Lopez", true, null, "Lopez", "", false, "$2a$11$PrmaGZ2yb3pTkTt.BrZQ1.o3WLMxM711xd776JlzcsHFu1mHnQOnO", "Doctor", "", "", "doctor" }
                });

            migrationBuilder.InsertData(
                table: "DoctorSchedules",
                columns: new[] { "Id", "DayOfWeek", "DoctorId", "EndTime", "IsAvailable", "StartTime" },
                values: new object[,]
                {
                    { 8, 0, 2, "16:00", false, "09:00" },
                    { 9, 1, 2, "16:00", true, "09:00" },
                    { 10, 2, 2, "16:00", true, "09:00" },
                    { 11, 3, 2, "16:00", true, "09:00" },
                    { 12, 4, 2, "16:00", true, "09:00" },
                    { 13, 5, 2, "16:00", true, "09:00" },
                    { 14, 6, 2, "16:00", true, "09:00" }
                });

            migrationBuilder.InsertData(
                table: "Doctors",
                columns: new[] { "Id", "Bio", "ConsultationFee", "ContactNumber", "CreatedAt", "DoctorCode", "Email", "FirstName", "FollowUpFee", "Gender", "IsActive", "LastName", "MiddleName", "PrcLicenseExpiry", "PrcLicenseNumber", "PtrNumber", "S2LicenseNumber", "SignatureContentType", "SignatureFileName", "SignatureImage", "SignatureUploadedAt", "SpecialistFee", "Specialty", "SubSpecialty", "Suffix", "TinNumber", "UserId" },
                values: new object[] { 1, "", 800m, "09171112222", new DateTime(2026, 10, 15, 3, 28, 6, 697, DateTimeKind.Utc).AddTicks(6719), "DR-0001", "mlopez@procreate.ai", "Maria", 500m, "Female", true, "Lopez", "", null, "0123456", "", "", "", "", null, null, 1200m, "Obstetrics and Gynecology", "Reproductive Endocrinology", "", "", 3 });

            migrationBuilder.InsertData(
                table: "InventoryItems",
                columns: new[] { "Id", "BrandName", "CategoryId", "ConversionFactor", "CostPrice", "CreatedAt", "CurrentStock", "Description", "Dosage", "IsActive", "ItemType", "MinOrderQty", "Name", "ReorderLevel", "SellingPrice", "Sku", "SubUnit", "SupplierId", "UnitOfMeasure", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Aquasonic", 1, null, 180m, new DateTime(2026, 10, 15, 3, 28, 6, 755, DateTimeKind.Utc).AddTicks(9929), 24, "", "", true, "Consumable", 6, "Ultrasound Gel", 10, 250m, "US-GEL-01", "", 1, "Bottle", new DateTime(2026, 10, 15, 3, 28, 6, 755, DateTimeKind.Utc).AddTicks(9931) },
                    { 2, "Fit One", 1, null, 5m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(7853), 139, "", "", true, "Consumable", 100, "Probe Cover", 50, 12m, "US-PC-01", "", 1, "Piece", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(7856) },
                    { 3, "Indoplas", 2, null, 25m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(7871), 99, "", "", true, "Consumable", 10, "Cotton Balls", 20, 40m, "PH-CB-01", "", 1, "Pack", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(7871) },
                    { 4, "BD", 2, 100, 850m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(7938), 6, "", "", true, "Consumable", 2, "Vacutainer Tube (EDTA)", 8, 1100m, "PH-VT-01", "Piece", 1, "Box", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(7939) },
                    { 5, "Biogesic", 3, 100, 150m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9088), 0, "", "500mg", true, "Medicine", 2, "Paracetamol", 5, 300m, "MED-001", "Tablet", 2, "Box", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9089) },
                    { 6, "Dolfenal", 3, 50, 140m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9631), 0, "", "500mg", true, "Medicine", 2, "Mefenamic Acid", 5, 240m, "MED-002", "Capsule", 2, "Box", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9632) },
                    { 7, "Norvasc", 3, 30, 150m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9638), 40, "", "50mg", true, "Medicine", 5, "Amlodipine", 10, 300m, "MED-003", "Tablet", 2, "Box", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9639) },
                    { 8, "Sartan", 3, 30, 150m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9644), 4, "", "50mg", true, "Medicine", 5, "Losartan", 10, 300m, "MED-004", "Tablet", 2, "Box", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9645) },
                    { 9, "Littmann", null, null, 5000m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9650), 3, "", "", true, "Asset", 1, "Stethoscope", 1, 0m, "AST-001", "", 1, "Piece", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9651) },
                    { 10, "Omron", null, null, 3200m, new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9655), 2, "", "", true, "Asset", 1, "Digital BP Monitor", 1, 0m, "AST-002", "", 1, "Piece", new DateTime(2026, 10, 15, 3, 28, 6, 756, DateTimeKind.Utc).AddTicks(9656) }
                });

            migrationBuilder.InsertData(
                table: "LabTests",
                columns: new[] { "Id", "CategoryId", "Code", "IsActive", "Method", "Name", "Price", "ProductId", "Specimen", "TurnaroundHours" },
                values: new object[,]
                {
                    { 1, 1, "CBC", true, "Automated", "Complete Blood Count", 350m, null, "Blood", 2 },
                    { 2, 2, "FBS", true, "Enzymatic", "Fasting Blood Sugar", 150m, null, "Blood", 1 },
                    { 3, 2, "CREA", true, "Jaffe", "Creatinine", 150m, null, "Blood", 1 },
                    { 4, 2, "UA", true, "Enzymatic", "Uric Acid", 150m, null, "Blood", 1 },
                    { 5, 3, "URA", true, "Dipstick + Microscopy", "Urinalysis", 120m, null, "Urine", 1 },
                    { 6, 4, "HBsAg", true, "ELISA", "Hepatitis B Surface Antigen", 450m, null, "Blood", 4 },
                    { 7, 5, "CXR-PA", true, "Digital Radiography", "Chest X-Ray (PA)", 500m, null, "None", 2 },
                    { 8, 5, "CXR-APL", true, "Digital Radiography", "Chest X-Ray (APL)", 650m, null, "None", 2 },
                    { 9, 5, "PXR", true, "Digital Radiography", "Pelvic X-Ray", 600m, null, "None", 2 },
                    { 10, 6, "TVS", true, "Sonography", "Transvaginal Ultrasound", 1500m, null, "None", 2 },
                    { 11, 6, "PUS", true, "Sonography", "Pelvic Ultrasound", 1300m, null, "None", 2 },
                    { 12, 6, "BUS", true, "Sonography", "Breast Ultrasound", 1400m, null, "None", 2 },
                    { 13, 6, "FOLM", true, "Sonography", "Follicle Monitoring", 1200m, null, "None", 1 },
                    { 14, 7, "ECG", true, "Electrocardiography", "12-Lead ECG", 450m, null, "None", 1 },
                    { 15, 7, "2DECHO", true, "Doppler Echocardiography", "2D Echocardiogram", 3500m, null, "None", 4 }
                });

            migrationBuilder.InsertData(
                table: "DoctorSchedules",
                columns: new[] { "Id", "DayOfWeek", "DoctorId", "EndTime", "IsAvailable", "StartTime" },
                values: new object[,]
                {
                    { 1, 0, 1, "17:00", false, "08:00" },
                    { 2, 1, 1, "17:00", true, "08:00" },
                    { 3, 2, 1, "17:00", true, "08:00" },
                    { 4, 3, 1, "17:00", true, "08:00" },
                    { 5, 4, 1, "17:00", true, "08:00" },
                    { 6, 5, 1, "17:00", true, "08:00" },
                    { 7, 6, 1, "17:00", false, "08:00" }
                });

            migrationBuilder.InsertData(
                table: "ServiceInventoryItems",
                columns: new[] { "Id", "InventoryItemId", "ProductId", "Quantity" },
                values: new object[,]
                {
                    { 1, 1, 2, 1 },
                    { 2, 2, 2, 1 },
                    { 3, 3, 1, 1 },
                    { 4, 4, 1, 1 }
                });

            migrationBuilder.InsertData(
                table: "StockBatches",
                columns: new[] { "Id", "BatchNumber", "ExpiryDate", "ExpiryNoticeStage", "InventoryItemId", "QuantityReceived", "QuantityRemaining", "ReceivedAt" },
                values: new object[,]
                {
                    { 1, "GEL-A10", new DateTime(2027, 1, 3, 0, 0, 0, 0, DateTimeKind.Local), 0, 1, 24, 24, new DateTime(2026, 10, 15, 3, 28, 6, 758, DateTimeKind.Utc).AddTicks(1190) },
                    { 2, "EDTA-2411", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Local), 0, 4, 6, 6, new DateTime(2026, 10, 15, 3, 28, 6, 763, DateTimeKind.Utc).AddTicks(8338) },
                    { 3, "AML-2402", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Local), 0, 7, 40, 40, new DateTime(2026, 10, 15, 3, 28, 6, 763, DateTimeKind.Utc).AddTicks(8518) },
                    { 4, "LOS-2603", new DateTime(2026, 12, 4, 0, 0, 0, 0, DateTimeKind.Local), 0, 8, 4, 4, new DateTime(2026, 10, 15, 3, 28, 6, 763, DateTimeKind.Utc).AddTicks(8525) }
                });

            migrationBuilder.InsertData(
                table: "TestInventoryItems",
                columns: new[] { "Id", "InventoryItemId", "LabTestId", "Quantity" },
                values: new object[,]
                {
                    { 1, 4, 1, 1 },
                    { 2, 3, 1, 1 },
                    { 3, 4, 2, 1 },
                    { 4, 4, 3, 1 },
                    { 5, 1, 10, 1 },
                    { 6, 2, 10, 1 },
                    { 7, 1, 11, 1 },
                    { 8, 2, 11, 1 }
                });

            migrationBuilder.InsertData(
                table: "TestPanelItems",
                columns: new[] { "Id", "LabTestId", "PanelId" },
                values: new object[,]
                {
                    { 1, 1, 1 },
                    { 2, 5, 1 },
                    { 3, 2, 1 },
                    { 4, 3, 1 },
                    { 5, 4, 1 },
                    { 6, 7, 1 },
                    { 7, 14, 1 },
                    { 8, 1, 2 },
                    { 9, 5, 2 },
                    { 10, 2, 2 },
                    { 11, 7, 2 },
                    { 12, 2, 3 },
                    { 13, 3, 3 },
                    { 14, 4, 3 },
                    { 15, 1, 4 }
                });

            migrationBuilder.InsertData(
                table: "TestParameters",
                columns: new[] { "Id", "LabTestId", "Name", "NormalMax", "NormalMin", "ReferenceRange", "Unit" },
                values: new object[,]
                {
                    { 1, 1, "WBC", "11.0", "4.5", "4.5-11.0", "10^9/L" },
                    { 2, 1, "RBC", "5.5", "4.5", "4.5-5.5", "10^12/L" },
                    { 3, 1, "Hemoglobin", "17.5", "13.5", "13.5-17.5", "g/dL" },
                    { 4, 1, "Hematocrit", "53", "41", "41-53", "%" },
                    { 5, 1, "Platelets", "400", "150", "150-400", "10^9/L" },
                    { 6, 2, "Glucose", "100", "70", "70-100", "mg/dL" },
                    { 7, 3, "Creatinine", "1.3", "0.7", "0.7-1.3", "mg/dL" },
                    { 8, 4, "Uric Acid", "7.0", "3.4", "3.4-7.0", "mg/dL" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBatches_BatchDate_StartTime",
                table: "AppointmentBatches",
                columns: new[] { "BatchDate", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBatchTemplates_DayOfWeek_StartTime",
                table: "AppointmentBatchTemplates",
                columns: new[] { "DayOfWeek", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BatchId_QueuePosition",
                table: "Appointments",
                columns: new[] { "BatchId", "QueuePosition" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ScheduledAt",
                table: "Appointments",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_VisitId",
                table: "Bills",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomRoles_Name",
                table: "CustomRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_UserId",
                table: "Doctors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_DoctorId",
                table: "DoctorSchedules",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CategoryId",
                table: "InventoryItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SupplierId",
                table: "InventoryItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_LabTestId",
                table: "LabOrders",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_VisitId",
                table: "LabOrders",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_LabOrderId",
                table: "LabResults",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_TestParameterId",
                table: "LabResults",
                column: "TestParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_CategoryId",
                table: "LabTests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_ProductId",
                table: "LabTests",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginActivities_CreatedAt",
                table: "LoginActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_CertificateNumber",
                table: "MedicalCertificates",
                column: "CertificateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_DoctorId",
                table: "MedicalCertificates",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_PatientId",
                table: "MedicalCertificates",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PatientId",
                table: "Orders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAllergies_PatientId",
                table: "PatientAllergies",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientConditions_PatientId",
                table: "PatientConditions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_PatientId",
                table: "PatientDocuments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_ResultDate",
                table: "PatientDocuments",
                column: "ResultDate");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedications_PatientId",
                table: "PatientMedications",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_PatientId",
                table: "QueueEntries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_QueueDate_QueueNumber",
                table: "QueueEntries",
                columns: new[] { "QueueDate", "QueueNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultDeliveries_DoctorId",
                table: "ResultDeliveries",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultDeliveries_PatientId_CreatedAt",
                table: "ResultDeliveries",
                columns: new[] { "PatientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInventoryItems_InventoryItemId",
                table: "ServiceInventoryItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInventoryItems_ProductId_InventoryItemId",
                table: "ServiceInventoryItems",
                columns: new[] { "ProductId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_ExpiryDate",
                table: "StockBatches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_InventoryItemId",
                table: "StockBatches",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CreatedAt",
                table: "StockMovements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryItemId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "InventoryItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TestInventoryItems_InventoryItemId",
                table: "TestInventoryItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TestInventoryItems_LabTestId_InventoryItemId",
                table: "TestInventoryItems",
                columns: new[] { "LabTestId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestPanelItems_LabTestId",
                table: "TestPanelItems",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_TestPanelItems_PanelId_LabTestId",
                table: "TestPanelItems",
                columns: new[] { "PanelId", "LabTestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestParameters_LabTestId",
                table: "TestParameters",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomRoleId",
                table: "Users",
                column: "CustomRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_PatientId",
                table: "Visits",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSignRecords_PatientId",
                table: "VitalSignRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSignRecords_RecordedAt",
                table: "VitalSignRecords",
                column: "RecordedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentBatchTemplates");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "DoctorSchedules");

            migrationBuilder.DropTable(
                name: "LabResults");

            migrationBuilder.DropTable(
                name: "LoginActivities");

            migrationBuilder.DropTable(
                name: "MedicalCertificates");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PatientAllergies");

            migrationBuilder.DropTable(
                name: "PatientConditions");

            migrationBuilder.DropTable(
                name: "PatientDocuments");

            migrationBuilder.DropTable(
                name: "PatientMedications");

            migrationBuilder.DropTable(
                name: "QueueEntries");

            migrationBuilder.DropTable(
                name: "ResultDeliveries");

            migrationBuilder.DropTable(
                name: "ServiceInventoryItems");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "TestInventoryItems");

            migrationBuilder.DropTable(
                name: "TestPanelItems");

            migrationBuilder.DropTable(
                name: "VitalSignRecords");

            migrationBuilder.DropTable(
                name: "AppointmentBatches");

            migrationBuilder.DropTable(
                name: "LabOrders");

            migrationBuilder.DropTable(
                name: "TestParameters");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "StockBatches");

            migrationBuilder.DropTable(
                name: "TestPanels");

            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.DropTable(
                name: "LabTests");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "TestCategories");

            migrationBuilder.DropTable(
                name: "CustomRoles");

            migrationBuilder.DropTable(
                name: "InventoryCategories");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
