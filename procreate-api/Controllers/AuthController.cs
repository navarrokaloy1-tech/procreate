using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// The role carried by a patient token. Not one of the staff roles, so a
    /// patient can never satisfy a staff-only policy by accident.
    /// </summary>
    public const string PatientRole = "Patient";

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // ----------------------------------------------------------
    // Staff
    // ----------------------------------------------------------

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password" });

        // Drives the online/offline dot on the account list.
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = IssueToken(
            subject: user.Id,
            name: user.Username,
            role: user.Role,
            fullName: user.FullName);

        return Ok(new
        {
            token,
            user = new { user.Id, user.Username, user.FullName, user.Role, user.Email, user.MustChangePassword }
        });
    }

    // ----------------------------------------------------------
    // Patients
    // ----------------------------------------------------------

    /// <summary>
    /// Portal sign-in. The patient identifies themselves by the email or
    /// mobile number on their record, whichever they registered with.
    /// </summary>
    [HttpPost("patient-login")]
    public async Task<IActionResult> PatientLogin([FromBody] LoginRequest req)
    {
        var identifier = (req.Username ?? string.Empty).Trim();
        if (identifier.Length == 0 || string.IsNullOrEmpty(req.Password))
            return Unauthorized(new { message = "Invalid sign-in details" });

        var patient = await _db.Patients.FirstOrDefaultAsync(p =>
            p.PortalEnabled &&
            (p.Email == identifier || p.ContactNumber == identifier || p.PatientCode == identifier));

        // Same reply whichever half is wrong, so this cannot be used to work
        // out which numbers and addresses are registered.
        if (patient is null ||
            patient.PasswordHash.Length == 0 ||
            !BCrypt.Net.BCrypt.Verify(req.Password, patient.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid sign-in details" });
        }

        return Ok(await IssuePatientSessionAsync(patient));
    }

    /// <summary>
    /// Portal sign-in by scanning the patient card. The QR carries CardToken,
    /// a random secret — never the patient code, which runs in sequence and
    /// could therefore be guessed by counting.
    /// </summary>
    [HttpPost("patient-login-qr")]
    public async Task<IActionResult> PatientLoginQr([FromBody] CardLoginRequest req)
    {
        var token = NormaliseCardPayload(req.Card);
        if (token.Length == 0)
            return Unauthorized(new { message = "That card could not be read." });

        var patient = await _db.Patients
            .FirstOrDefaultAsync(p => p.PortalEnabled && p.CardToken == token);

        if (patient is null)
            return Unauthorized(new { message = "That card is not linked to an active account." });

        return Ok(await IssuePatientSessionAsync(patient));
    }

    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { message = "Logged out" });

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private async Task<object> IssuePatientSessionAsync(Patient patient)
    {
        patient.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var fullName = string.Join(' ', new[] { patient.FirstName, patient.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        var token = IssueToken(
            subject: patient.Id,
            name: patient.PatientCode,
            role: PatientRole,
            fullName: fullName);

        return new
        {
            token,
            user = new
            {
                patient.Id,
                username = patient.PatientCode,
                fullName,
                role = PatientRole,
                patient.Email,
                patient.PatientCode
            }
        };
    }

    private string IssueToken(int subject, string name, string role, string fullName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, subject.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role),
            new Claim("fullName", fullName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Strips the "procreate-card:" scheme a printed card carries, so the
    /// scanner can hand the raw payload straight over.
    /// </summary>
    private static string NormaliseCardPayload(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        const string scheme = "procreate-card:";
        return trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            ? trimmed[scheme.Length..].Trim()
            : trimmed;
    }

    /// <summary>A URL-safe random secret for a patient card.</summary>
    internal static string NewCardToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

public record LoginRequest(string Username, string Password);
public record CardLoginRequest(string? Card);
