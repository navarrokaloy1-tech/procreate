using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ProCreateApi.Controllers;

/// <summary>
/// Login accounts, their roles and access. An administrator issues a
/// temporary password on create and on reset; it is returned exactly once and
/// only ever stored hashed, so nothing here can hand a password back later.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    /// <summary>An account counts as online if it authenticated this recently.</summary>
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Roles the console understands. The sidebar is filtered on these exact
    /// strings, so a role outside this set would leave its holder with no nav.
    /// </summary>
    private static readonly Dictionary<string, string> RoleLabels = new()
    {
        ["Admin"] = "Clinic Administrator",
        ["Doctor"] = "Doctor",
        ["Staff"] = "Staff",
        ["Cashier"] = "Cashier"
    };

    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet("roles")]
    public IActionResult GetRoles() =>
        Ok(RoleLabels.Select(r => new { value = r.Key, label = r.Value }));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // LIKE rather than Contains, which SQLite makes case-sensitive.
            var term = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.Like(u.FullName, term) ||
                EF.Functions.Like(u.Username, term) ||
                EF.Functions.Like(u.Email, term));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, currentUserId = ActingUserId(), data = users.Select(Project) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? NotFound() : Ok(Project(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserRequest request)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        var username = BuildUsername(request);
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { message = $"The username \"{username}\" is already taken." });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email.Trim()))
            return BadRequest(new { message = $"{request.Email.Trim()} already has an account." });

        var temporaryPassword = GenerateTemporaryPassword();

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            MustChangePassword = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        Apply(user, request);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // The only time this value exists in the clear. It is not stored and
        // cannot be read back, so the administrator has to pass it on now.
        return Ok(new
        {
            user = Project(user),
            temporaryPassword,
            message = "Account created. Give the temporary password to the account holder — it cannot be shown again."
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email.Trim() && u.Id != id))
            return BadRequest(new { message = $"{request.Email.Trim()} already has an account." });

        // Demoting or deactivating the only administrator would lock everyone
        // out of user management, with no way back in through the UI.
        if (user.Role == "Admin" && (request.Role != "Admin" || !request.IsActive)
            && await IsLastActiveAdmin(id))
        {
            return BadRequest(new { message = "This is the only active administrator. Promote another account first." });
        }

        Apply(user, request);
        await _db.SaveChangesAsync();

        return Ok(Project(user));
    }

    /// <summary>Issues a fresh temporary password, returned once.</summary>
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var temporaryPassword = GenerateTemporaryPassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        user.MustChangePassword = true;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            temporaryPassword,
            message = $"A temporary password was issued for {user.Username}. It cannot be shown again."
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (ActingUserId() == id)
            return BadRequest(new { message = "You cannot delete the account you are signed in with." });

        if (await IsLastActiveAdmin(id))
            return BadRequest(new { message = "This is the only active administrator. Promote another account first." });

        // A doctor record may point at this login, and the practitioner should
        // outlive their system access; the FK is configured to null out.
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private async Task<bool> IsLastActiveAdmin(int id) =>
        !await _db.Users.AnyAsync(u => u.Role == "Admin" && u.IsActive && u.Id != id);

    /// <summary>The signed-in account, from the bearer token when one was sent.</summary>
    private int? ActingUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string? Validate(UserRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName)) return "A first name is required.";
        if (string.IsNullOrWhiteSpace(r.Email)) return "An email address is required.";
        if (!r.Email.Contains('@')) return "Enter a valid email address.";
        if (!RoleLabels.ContainsKey(r.Role ?? "")) return "Choose a valid role.";
        return null;
    }

    private static void Apply(User user, UserRequest r)
    {
        user.FirstName = r.FirstName.Trim();
        user.MiddleName = r.MiddleName?.Trim() ?? string.Empty;
        user.LastName = r.LastName?.Trim() ?? string.Empty;
        user.Suffix = r.Suffix?.Trim() ?? string.Empty;
        user.Sex = r.Sex?.Trim() ?? string.Empty;
        user.Birthday = r.Birthday;
        user.ContactNumber = r.ContactNumber?.Trim() ?? string.Empty;
        user.Address = r.Address?.Trim() ?? string.Empty;
        user.Email = r.Email.Trim();
        user.Role = r.Role!;
        user.IsActive = r.IsActive;
        user.FullName = string.Join(' ', new[] { user.FirstName, user.LastName, user.Suffix }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    /// <summary>first.last, with a numeric suffix when that is already taken.</summary>
    private string BuildUsername(UserRequest r)
    {
        var basis = string.Join('.', new[] { r.FirstName, r.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => new string(part!.Trim().ToLowerInvariant()
                    .Where(char.IsLetterOrDigit).ToArray())))
            .Trim('.');

        if (string.IsNullOrWhiteSpace(basis)) basis = "user";

        var candidate = basis;
        var suffix = 1;
        while (_db.Users.Any(u => u.Username == candidate))
            candidate = $"{basis}{++suffix}";

        return candidate;
    }

    /// <summary>
    /// Twelve characters from a deliberately unambiguous alphabet — no O/0 or
    /// I/l — because this gets read aloud or written on paper.
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    /// <summary>Never includes the password hash.</summary>
    private static object Project(User u) => new
    {
        u.Id,
        u.Username,
        u.FullName,
        u.FirstName,
        u.MiddleName,
        u.LastName,
        u.Suffix,
        u.Sex,
        u.Birthday,
        u.ContactNumber,
        u.Address,
        u.Email,
        u.Role,
        roleLabel = RoleLabels.TryGetValue(u.Role, out var label) ? label : u.Role,
        u.IsActive,
        u.MustChangePassword,
        u.CreatedAt,
        u.LastLoginAt,
        isOnline = u.LastLoginAt is not null && DateTime.UtcNow - u.LastLoginAt < OnlineWindow
    };
}

public record UserRequest(
    string FirstName,
    string? MiddleName,
    string? LastName,
    string? Suffix,
    string? Sex,
    DateTime? Birthday,
    string? ContactNumber,
    string? Address,
    string Email,
    string? Role,
    bool IsActive);
