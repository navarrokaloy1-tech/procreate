namespace ProCreateApi.Models;

public class Patient
{
    public int Id { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string BloodType { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactNumber { get; set; } = string.Empty;
    public string? CivilStatus { get; set; }
    public string? Nationality { get; set; }
    public string? Occupation { get; set; }
    public string? Suffix { get; set; }
    public string? Landline { get; set; }

    // Structured address. Address holds the street line; Region/City store the
    // display name rather than a code so the record stays readable on its own.
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? ZipCode { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactNotes { get; set; }
    public string? PhotoUrl { get; set; }

    // Insurance / statutory discount identifiers. Senior citizen and PWD IDs
    // both entitle the holder to a 20% discount at billing time.
    public string? PhilHealthNumber { get; set; }
    public string? SeniorCitizenId { get; set; }
    public string? PwdId { get; set; }
    public string? HmoProvider { get; set; }
    public string? HmoAccountNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Visit> Visits { get; set; } = new();
    // LIS integration fields
    public DateTime? LisRegisteredAt { get; set; }  // when ADT^A01 was last sent
}

public class Visit
{
    public int Id { get; set; }
    public string VisitCode { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    public string ReferringPhysician { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public string PaymentMethod { get; set; } = string.Empty;
    public List<LabOrder> LabOrders { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TestCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<LabTest> Tests { get; set; } = new();
}

public class LabTest
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public TestCategory Category { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Specimen { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TurnaroundHours { get; set; }
    public List<TestParameter> Parameters { get; set; } = new();
}

public class TestParameter
{
    public int Id { get; set; }
    public int LabTestId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public string? NormalMin { get; set; }
    public string? NormalMax { get; set; }
}

public class LabOrder
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Visit Visit { get; set; } = null!;
    public int LabTestId { get; set; }
    public LabTest LabTest { get; set; } = null!;
    public string Status { get; set; } = "Ordered";
    public string SpecimenBarcode { get; set; } = string.Empty;
    public DateTime? CollectedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public List<LabResult> Results { get; set; } = new();
    // LIS integration fields
    public string? LisStatus { get; set; }         // null | Sent | Acknowledged | Failed
    public string? LisOrderId { get; set; }        // filler order number echoed back by LIS
    public DateTime? LisSentAt { get; set; }
    public DateTime? LisAckedAt { get; set; }
    public string? LisError { get; set; }
}

public class LabResult
{
    public int Id { get; set; }
    public int LabOrderId { get; set; }
    public LabOrder LabOrder { get; set; } = null!;
    public int TestParameterId { get; set; }
    public TestParameter TestParameter { get; set; } = null!;
    public string Value { get; set; } = string.Empty;
    public string Flag { get; set; } = "Normal";
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Bill
{
    public int Id { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public int VisitId { get; set; }
    public Visit Visit { get; set; } = null!;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Change { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Order
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public string Status { get; set; } = "Draft"; // Draft | Ordered | Cancelled | Completed
    public string Referrer { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// A walk-in ticket. QueueNumber restarts at 1 each QueueDate, so the pair
/// (QueueDate, QueueNumber) is what the printed slip and the display board show.
/// </summary>
public class QueueEntry
{
    public int Id { get; set; }
    public int QueueNumber { get; set; }
    public DateTime QueueDate { get; set; }
    public string PatientName { get; set; } = string.Empty;
    /// <summary>Set when the walk-in matches an existing registered patient.</summary>
    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string Status { get; set; } = "Waiting"; // Waiting | Called | Served | Skipped
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CalledAt { get; set; }
}

public class Doctor
{
    public int Id { get; set; }
    public string DoctorCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string SubSpecialty { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;

    // Licensing
    public string PrcLicenseNumber { get; set; } = string.Empty;
    public DateTime? PrcLicenseExpiry { get; set; }
    public string PtrNumber { get; set; } = string.Empty;
    public string S2LicenseNumber { get; set; } = string.Empty;
    public string TinNumber { get; set; } = string.Empty;

    // Practice
    public decimal ConsultationFee { get; set; }
    public decimal FollowUpFee { get; set; }
    public decimal SpecialistFee { get; set; }
    public string Bio { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    /// <summary>Optional link to a login account, granting the doctor system access.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }
    public List<DoctorSchedule> Schedules { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One row per weekday defining a doctor's available hours.</summary>
public class DoctorSchedule
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    /// <summary>0 = Sunday through 6 = Saturday, matching <see cref="System.DayOfWeek"/>.</summary>
    public int DayOfWeek { get; set; }
    public bool IsAvailable { get; set; } = true;
    /// <summary>Wall-clock times stored as "HH:mm" — no timezone conversion wanted.</summary>
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "17:00";
}

/// <summary>
/// A certificate issued to a patient or to an unregistered walk-in. Exactly one
/// of PatientId / WalkInName identifies the recipient — see IssuedTo.
/// </summary>
public class MedicalCertificate
{
    public int Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;

    /// <summary>Set when issued to a registered patient.</summary>
    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Set instead of PatientId for an express walk-in.</summary>
    public string WalkInName { get; set; } = string.Empty;
    public string WalkInAge { get; set; } = string.Empty;
    public string WalkInAddress { get; set; } = string.Empty;

    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public DateTime IssueDate { get; set; } = DateTime.Today;
    /// <summary>General | Work | School</summary>
    public string Template { get; set; } = "General";

    public string Diagnosis { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    /// <summary>Internal or footer notes, not usually printed in the body.</summary>
    public string Remarks { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Appointment
{
    public int Id { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public string Service { get; set; } = "General Consultation";
    public DateTime ScheduledAt { get; set; }
    public string Type { get; set; } = "Scheduled";   // Scheduled | Walk-in | Follow-up
    /// <summary>Scheduled | Confirmed | CheckedIn | InProgress | Completed | Cancelled | NoShow | Pending</summary>
    public string Status { get; set; } = "Scheduled";
    public string ChiefComplaint { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
