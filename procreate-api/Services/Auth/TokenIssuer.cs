using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ProCreateApi.Services.Auth;

/// <summary>
/// Mints the application's own session token. Every sign-in route — staff
/// password, patient password, single sign-on — ends here, so a session looks
/// the same to the rest of the API however it was started.
/// </summary>
public class TokenIssuer
{
    /// <summary>
    /// The role carried by a patient token. Not one of the staff roles, so a
    /// patient can never satisfy a staff-only policy by accident.
    /// </summary>
    public const string PatientRole = "Patient";

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public TokenIssuer(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Starts a staff session and stamps the login time.</summary>
    public async Task<object> CreateStaffSessionAsync(User user)
    {
        // Drives the online/offline dot on the account list.
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = IssueToken(
            subject: user.Id,
            name: user.Username,
            role: user.Role,
            fullName: user.FullName);

        return new
        {
            token,
            user = new { user.Id, user.Username, user.FullName, user.Role, user.Email, user.MustChangePassword }
        };
    }

    /// <summary>Starts a patient portal session and stamps the login time.</summary>
    public async Task<object> CreatePatientSessionAsync(Patient patient)
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
}
