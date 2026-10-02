using ProCreateApi.Data;
using ProCreateApi.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// Password sign-in, for staff and for patients. Single sign-on lives in
/// <see cref="AuthSsoController"/>; both end at the same
/// <see cref="TokenIssuer"/>, so the session that comes out is identical.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenIssuer _tokens;
    private readonly LoginAudit _audit;

    public AuthController(AppDbContext db, TokenIssuer tokens, LoginAudit audit)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
    }

    // ----------------------------------------------------------
    // Staff
    // ----------------------------------------------------------

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            await _audit.RecordFailureAsync(HttpContext, "Password", "Staff",
                req.Username, "Invalid username or password");
            return Unauthorized(new { message = "Invalid username or password" });
        }

        var session = await _tokens.CreateStaffSessionAsync(user);
        await _audit.RecordSuccessAsync(HttpContext, "Password", "Staff",
            req.Username, user.Id, user.FullName, user.Role);
        return Ok(session);
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
        {
            await _audit.RecordFailureAsync(HttpContext, "Password", "Patient",
                identifier, "Missing sign-in details");
            return Unauthorized(new { message = "Invalid sign-in details" });
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p =>
            p.PortalEnabled &&
            (p.Email == identifier || p.ContactNumber == identifier || p.PatientCode == identifier));

        // Same reply whichever half is wrong, so this cannot be used to work
        // out which numbers and addresses are registered.
        if (patient is null ||
            patient.PasswordHash.Length == 0 ||
            !BCrypt.Net.BCrypt.Verify(req.Password, patient.PasswordHash))
        {
            await _audit.RecordFailureAsync(HttpContext, "Password", "Patient",
                identifier, "Invalid sign-in details");
            return Unauthorized(new { message = "Invalid sign-in details" });
        }

        var session = await _tokens.CreatePatientSessionAsync(patient);
        var patientName = string.Join(' ', new[] { patient.FirstName, patient.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        await _audit.RecordSuccessAsync(HttpContext, "Password", "Patient",
            identifier, patient.Id, patientName, TokenIssuer.PatientRole);
        return Ok(session);
    }

    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { message = "Logged out" });
}

public record LoginRequest(string Username, string Password);
