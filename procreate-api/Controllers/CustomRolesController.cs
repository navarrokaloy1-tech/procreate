using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// Clinic-defined roles. Each grants a set of features — the sidebar
/// destinations an account assigned to it can see. Admin-only: these decide who
/// sees what.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/custom-roles")]
public class CustomRolesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CustomRolesController(AppDbContext db) => _db = db;

    /// <summary>The features a role can grant, for the role editor's checkboxes.</summary>
    [HttpGet("features")]
    public IActionResult GetFeatures() =>
        Ok(CustomRoleFeatures.Labels.Select(f => new { key = f.Key, label = f.Value }));

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _db.CustomRoles.OrderBy(r => r.Name).ToListAsync();
        var counts = await _db.Users
            .Where(u => u.CustomRoleId != null)
            .GroupBy(u => u.CustomRoleId!.Value)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(roles.Select(r => new
        {
            r.Id,
            r.Name,
            r.Description,
            features = r.FeatureList(),
            users = counts.FirstOrDefault(c => c.RoleId == r.Id)?.Count ?? 0,
            r.CreatedAt
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomRoleRequest request)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        var name = request.Name.Trim();
        if (await _db.CustomRoles.AnyAsync(r => r.Name == name))
            return BadRequest(new { message = $"A role named \"{name}\" already exists." });

        var role = new CustomRole
        {
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            Features = CleanFeatures(request.Features)
        };
        _db.CustomRoles.Add(role);
        await _db.SaveChangesAsync();
        return Ok(new { role.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomRoleRequest request)
    {
        var role = await _db.CustomRoles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        var name = request.Name.Trim();
        if (await _db.CustomRoles.AnyAsync(r => r.Name == name && r.Id != id))
            return BadRequest(new { message = $"A role named \"{name}\" already exists." });

        role.Name = name;
        role.Description = request.Description?.Trim() ?? string.Empty;
        role.Features = CleanFeatures(request.Features);
        await _db.SaveChangesAsync();
        return Ok(new { role.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.CustomRoles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        // Members fall back to their built-in role (the FK nulls out on delete).
        _db.CustomRoles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private static string? Validate(CustomRoleRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "A role name is required.";
        var features = (r.Features ?? new List<string>()).Where(CustomRoleFeatures.IsValid).ToList();
        if (features.Count == 0) return "Select at least one feature.";
        return null;
    }

    /// <summary>Known keys only, de-duplicated, in the catalogue's own order.</summary>
    private static string CleanFeatures(List<string>? features)
    {
        var chosen = (features ?? new List<string>()).ToHashSet();
        var ordered = CustomRoleFeatures.All.Where(chosen.Contains);
        return string.Join(',', ordered);
    }
}

public record CustomRoleRequest(string Name, string? Description, List<string>? Features);
