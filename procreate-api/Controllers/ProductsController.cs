using ProCreateApi.Data;
using ProCreateApi.Services.Search;
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

        var products = await query.OrderBy(p => p.Name).ToListAsync();
        return Ok(products);
    }
}
