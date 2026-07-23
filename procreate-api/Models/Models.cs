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
    public string? EmergencyContactRelationship { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Visit> Visits { get; set; } = new();
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
