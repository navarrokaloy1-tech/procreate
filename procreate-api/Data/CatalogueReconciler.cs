using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Data;

/// <summary>
/// Brings the two halves of the catalogue together, once, at startup.
///
/// Services (Product) and orderable tests (LabTest) were seeded as independent
/// lists that only partly overlapped by name, so Service Management and the
/// order picker showed different counts. After this runs every orderable
/// service owns a test and every test that predates the rule has a service, so
/// the two screens agree and Service Management is the only place either is
/// edited. It is idempotent: it only ever touches rows that are still unlinked.
/// </summary>
public static class CatalogueReconciler
{
    public static void Reconcile(AppDbContext db)
    {
        LinkServicesToTests(db);
        BackfillServicesForTests(db);
    }

    private static void LinkServicesToTests(AppDbContext db)
    {
        var linkedProductIds = db.LabTests
            .Where(t => t.ProductId != null)
            .Select(t => t.ProductId!.Value)
            .ToHashSet();

        var services = db.Products
            .Where(p => !linkedProductIds.Contains(p.Id))
            .ToList();

        foreach (var service in services)
        {
            if (!ServiceCategories.DepartmentByCategory.TryGetValue(service.Category, out var department))
                continue;

            var test = db.LabTests.FirstOrDefault(t =>
                t.ProductId == null && t.Name.ToLower() == service.Name.ToLower());

            if (test is null)
            {
                test = new LabTest
                {
                    CategoryId = CategoryId(db, service.Category, department),
                    Code = service.Code,
                    Name = service.Name,
                    Specimen = string.Empty,
                    Method = string.Empty
                };
                db.LabTests.Add(test);
            }

            // The adopted test keeps its own code: that is what the analyser
            // sends back in an OBR, so renaming it would break LIS matching.
            test.ProductId = service.Id;
            test.Price = service.Price;
            test.IsActive = service.IsActive;
            db.SaveChanges();
        }
    }

    private static void BackfillServicesForTests(AppDbContext db)
    {
        var orphans = db.LabTests
            .Include(t => t.Category)
            .Where(t => t.ProductId == null)
            .ToList();

        foreach (var test in orphans)
        {
            // A test outside the four orderable departments cannot be priced as
            // a service, so it is left alone rather than guessed at.
            var category = ServiceCategories.CategoryForDepartment(test.Category?.Department);
            if (category is null) continue;

            var code = db.Products.Any(p => p.Code == test.Code) ? $"{test.Code}-{test.Id}" : test.Code;
            var service = new Product
            {
                Code = code,
                Name = test.Name,
                Category = category,
                Price = test.Price,
                IsActive = test.IsActive,
                SeniorPwdDiscount = true
            };

            db.Products.Add(service);
            db.SaveChanges();

            test.ProductId = service.Id;
            db.SaveChanges();
        }
    }

    private static int CategoryId(AppDbContext db, string name, string department)
    {
        var category = db.TestCategories.FirstOrDefault(c => c.Name == name && c.Department == department);
        if (category is null)
        {
            category = new TestCategory { Name = name, Department = department, Description = $"{name} services" };
            db.TestCategories.Add(category);
            db.SaveChanges();
        }

        return category.Id;
    }
}
