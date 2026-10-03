using ProCreateApi.Data;
using ProCreateApi.Services.Search;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// The billable service catalogue. Backed by Product, which is what the
/// cashier sells through Order/OrderItem — Service Management is the same
/// records seen from the clinic-setup side, so there is one price list.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServicesController(AppDbContext db) => _db = db;

    /// <summary>
    /// The categories the Add Service form offers, each saying whether a
    /// service in it becomes orderable or is only billed.
    /// </summary>
    [HttpGet("categories")]
    public IActionResult GetCategories() =>
        Ok(ServiceCategories.All.Select(name => new
        {
            name,
            orderable = ServiceCategories.DepartmentByCategory.ContainsKey(name),
            department = ServiceCategories.DepartmentByCategory.TryGetValue(name, out var d) ? d : null
        }));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.Products
            .Include(p => p.RequiredItems).ThenInclude(r => r.InventoryItem)
            .Include(p => p.LabTest!).ThenInclude(t => t.Parameters)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        // Unlike the cashier list this one shows retired services too, so the
        // setup screen can bring one back.
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.IsActive);
        else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => !p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            foreach (var token in SearchTerms.Tokenize(search))
            {
                var term = SearchTerms.Pattern(token);
                query = query.Where(p =>
                    EF.Functions.Like(p.Name, term, SearchTerms.Escape) ||
                    EF.Functions.Like(p.Code, term, SearchTerms.Escape) ||
                    EF.Functions.Like(p.Category, term, SearchTerms.Escape));
            }
        }

        var total = await query.CountAsync();
        var services = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = services.Select(Project) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var service = await LoadAsync(id);
        return service is null ? NotFound() : Ok(Project(service));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "A service name is required." });

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? await NextCodeAsync(request.Category)
            : request.Code.Trim();

        if (await _db.Products.AnyAsync(p => p.Code == code))
            return BadRequest(new { message = $"The code \"{code}\" is already in use." });

        var service = new Product
        {
            Code = code,
            Name = request.Name.Trim(),
            Category = request.Category?.Trim() ?? "Others",
            Price = request.Price,
            Description = request.Description?.Trim() ?? string.Empty,
            SeniorPwdDiscount = request.SeniorPwdDiscount,
            PhilHealthCovered = request.PhilHealthCovered,
            IsActive = request.IsActive
        };

        _db.Products.Add(service);
        await _db.SaveChangesAsync();
        await ReplaceRequiredItemsAsync(service.Id, request.RequiredItems);
        await SyncLabTestAsync(service, request.Parameters);

        var created = await LoadAsync(service.Id);
        return CreatedAtAction(nameof(GetById), new { id = service.Id }, Project(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceRequest request)
    {
        var service = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (service is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "A service name is required." });

        var code = string.IsNullOrWhiteSpace(request.Code) ? service.Code : request.Code.Trim();
        if (await _db.Products.AnyAsync(p => p.Code == code && p.Id != id))
            return BadRequest(new { message = $"The code \"{code}\" is already in use." });

        service.Code = code;
        service.Name = request.Name.Trim();
        service.Category = request.Category?.Trim() ?? "Others";
        service.Price = request.Price;
        service.Description = request.Description?.Trim() ?? string.Empty;
        service.SeniorPwdDiscount = request.SeniorPwdDiscount;
        service.PhilHealthCovered = request.PhilHealthCovered;
        service.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        await ReplaceRequiredItemsAsync(id, request.RequiredItems);
        await SyncLabTestAsync(service, request.Parameters);

        var saved = await LoadAsync(id);
        return Ok(Project(saved!));
    }

    /// <summary>
    /// Retires a service, or deletes it outright when nothing has been sold.
    /// A service named on a past order is kept so old bills still resolve.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (service is null) return NotFound();

        // Whichever way this goes the test stops being orderable, and it is
        // kept either way because past orders still point at it.
        var labTest = await _db.LabTests.FirstOrDefaultAsync(t => t.ProductId == id);
        if (labTest is not null) labTest.IsActive = false;

        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
        {
            service.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "The service has past orders, so it was deactivated instead of deleted.", deactivated = true });
        }

        _db.Products.Remove(service);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private Task<Product?> LoadAsync(int id) => _db.Products
        .AsNoTracking()
        .Include(p => p.RequiredItems).ThenInclude(r => r.InventoryItem)
        .Include(p => p.LabTest!).ThenInclude(t => t.Parameters)
        .FirstOrDefaultAsync(p => p.Id == id);

    private async Task ReplaceRequiredItemsAsync(int productId, List<RequiredItemInput>? items)
    {
        var existing = await _db.ServiceInventoryItems
            .Where(l => l.ProductId == productId).ToListAsync();
        _db.ServiceInventoryItems.RemoveRange(existing);

        // Same item twice would violate the unique index and only ever means
        // "use more of it", so the quantities are folded together.
        var merged = (items ?? new List<RequiredItemInput>())
            .Where(i => i.InventoryItemId > 0)
            .GroupBy(i => i.InventoryItemId)
            .Select(g => new ServiceInventoryItem
            {
                ProductId = productId,
                InventoryItemId = g.Key,
                Quantity = Math.Max(1, g.Sum(i => i.Quantity))
            });

        _db.ServiceInventoryItems.AddRange(merged);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Rewrites the parameter sheet. Rows are replaced wholesale because the
    /// list is ordered and editable in place, so matching them up by id would
    /// only reproduce what the client already sent.
    /// </summary>
    private async Task ReplaceParametersAsync(int labTestId, List<ServiceParameterInput>? parameters)
    {
        var existing = await _db.TestParameters
            .Where(p => p.LabTestId == labTestId).ToListAsync();
        _db.TestParameters.RemoveRange(existing);

        var rows = (parameters ?? new List<ServiceParameterInput>())
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new TestParameter
            {
                LabTestId = labTestId,
                Name = p.Name.Trim(),
                Unit = p.Unit?.Trim() ?? string.Empty,
                ReferenceRange = p.ReferenceRange?.Trim() ?? string.Empty,
                NormalMin = string.IsNullOrWhiteSpace(p.NormalMin) ? null : p.NormalMin.Trim(),
                NormalMax = string.IsNullOrWhiteSpace(p.NormalMax) ? null : p.NormalMax.Trim()
            });

        _db.TestParameters.AddRange(rows);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Keeps the orderable test in step with the catalogue entry. Service
    /// Management is the one place a test is named, priced and given its
    /// parameters, so the order picker and the LIS read what was set here.
    /// </summary>
    private async Task SyncLabTestAsync(Product service, List<ServiceParameterInput>? parameters)
    {
        var labTest = await _db.LabTests.FirstOrDefaultAsync(t => t.ProductId == service.Id);

        if (!ServiceCategories.DepartmentByCategory.TryGetValue(service.Category, out var department))
        {
            // A consultation or certificate is billed, not ordered. Any test
            // left over from an earlier category is retired rather than
            // deleted, because past orders still point at it.
            if (labTest is not null)
            {
                labTest.IsActive = false;
                await _db.SaveChangesAsync();
            }
            return;
        }

        // The seeded catalogue already holds the clinic's usual work, so adopt
        // a test of the same name instead of listing it twice in the picker.
        labTest ??= await _db.LabTests
            .FirstOrDefaultAsync(t => t.ProductId == null && t.Name.ToLower() == service.Name.ToLower());

        var currentDepartment = labTest is null
            ? null
            : await _db.TestCategories
                .Where(c => c.Id == labTest.CategoryId)
                .Select(c => c.Department)
                .FirstOrDefaultAsync();

        // Resolved before the test is attached: finding or creating the
        // category saves, which would otherwise flush a new test that has no
        // category yet and trip the foreign key.
        var categoryId = currentDepartment == department
            ? labTest!.CategoryId
            : await CategoryIdForAsync(service.Category, department);

        if (labTest is null)
        {
            // A new test takes the service's code; an adopted one keeps its
            // own, because that is what the analyser quotes back in an OBR.
            labTest = new LabTest { Code = service.Code, Specimen = string.Empty, Method = string.Empty };
            _db.LabTests.Add(labTest);
        }

        labTest.CategoryId = categoryId;
        labTest.ProductId = service.Id;
        labTest.Name = service.Name;
        labTest.Price = service.Price;
        labTest.IsActive = service.IsActive;

        await _db.SaveChangesAsync();
        await ReplaceParametersAsync(labTest.Id, parameters);
    }

    private async Task<int> CategoryIdForAsync(string name, string department)
    {
        var category = await _db.TestCategories
            .FirstOrDefaultAsync(c => c.Name == name && c.Department == department);

        if (category is null)
        {
            category = new TestCategory { Name = name, Department = department, Description = $"{name} services" };
            _db.TestCategories.Add(category);
            await _db.SaveChangesAsync();
        }

        return category.Id;
    }

    /// <summary>Next free code for a category, e.g. US-005.</summary>
    private async Task<string> NextCodeAsync(string? category)
    {
        var prefix = (category ?? "Others") switch
        {
            "Consultation" => "CON",
            "Laboratory" => "LAB",
            "Imaging" => "XR",
            "Ultrasound" => "US",
            "Heart Station" => "HS",
            "Procedure" => "PRO",
            "Vaccination" => "VAX",
            "Dental" => "DEN",
            "Physical Therapy" => "PT",
            _ => "MISC"
        };

        var codes = await _db.Products
            .Where(p => p.Code.StartsWith(prefix + "-"))
            .Select(p => p.Code)
            .ToListAsync();

        var highest = codes
            .Select(c => int.TryParse(c[(prefix.Length + 1)..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}-{highest + 1:D3}";
    }

    private static object Project(Product p) => new
    {
        p.Id,
        p.Code,
        p.Name,
        p.Category,
        p.Price,
        p.Description,
        p.SeniorPwdDiscount,
        p.PhilHealthCovered,
        p.IsActive,
        requiredItems = p.RequiredItems.Select(r => new
        {
            r.InventoryItemId,
            r.Quantity,
            name = r.InventoryItem?.Name ?? string.Empty,
            unit = r.InventoryItem?.UnitOfMeasure ?? string.Empty
        }),
        parameters = (p.LabTest?.Parameters ?? new List<TestParameter>())
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Unit,
                x.ReferenceRange,
                x.NormalMin,
                x.NormalMax
            }),
        orderable = p.LabTest is not null && p.LabTest.IsActive
    };
}

public record RequiredItemInput(int InventoryItemId, int Quantity);

public record ServiceParameterInput(
    string Name,
    string? Unit,
    string? ReferenceRange,
    string? NormalMin,
    string? NormalMax);

public record ServiceRequest(
    string Name,
    string? Code,
    string? Category,
    decimal Price,
    string? Description,
    bool SeniorPwdDiscount,
    bool PhilHealthCovered,
    bool IsActive,
    List<RequiredItemInput>? RequiredItems,
    List<ServiceParameterInput>? Parameters);
