using ProCreateApi.Data;
using ProCreateApi.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// Password sign-in, for staff and for patients. Single sign-on lives in
/// <see cref="AuthSsoController"/>; both end at the same
/// <see cref="TokenIssuer"/>, so the session that comes out is identical.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenIssuer _tokens;

    public AuthController(AppDbContext db, TokenIssuer tokens)
    {
        _db = db;
        _tokens = tokens;
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

        return Ok(await _tokens.CreateStaffSessionAsync(user));
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

        return Ok(await _tokens.CreatePatientSessionAsync(patient));
    }

    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { message = "Logged out" });
}

public record LoginRequest(string Username, string Password);
