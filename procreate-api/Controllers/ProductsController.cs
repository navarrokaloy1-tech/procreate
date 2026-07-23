using ProCreateApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var query = _db.Products.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search) || p.Category.Contains(search));

        var products = await query.OrderBy(p => p.Name).ToListAsync();
        return Ok(products);
    }
}
