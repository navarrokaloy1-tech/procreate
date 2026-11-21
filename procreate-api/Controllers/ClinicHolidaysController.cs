using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// The dates the clinic itself is shut — public holidays, company events.
///
/// A holiday blocks every doctor at once, which is the point: entered here
/// once rather than against each of them in turn. A doctor's own leave is
/// their days off, under <c>doctors/{id}/days-off</c>.
/// </summary>
[ApiController]
[Route("api/clinic-holidays")]
public class ClinicHolidaysController : ControllerBase
{
    private readonly AppDbContext _db;
    public ClinicHolidaysController(AppDbContext db) => _db = db;

    public record HolidayDto(int Id, DateTime Date, string Name);
    public record HolidayRequest(DateTime Date, string? Name);

    /// <summary>
    /// Upcoming closed dates. Past ones are left out unless asked for: they are
    /// history, and they would bury the ones that still stop a booking.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includePast = false)
    {
        var query = _db.ClinicHolidays.AsQueryable();
        if (!includePast) query = query.Where(h => h.Date >= DateTime.Today);

        var holidays = await query
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name))
            .ToListAsync();

        return Ok(new { total = holidays.Count, data = holidays });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] HolidayRequest request)
    {
        var date = request.Date.Date;
        if (date == default)
            return BadRequest(new { message = "A date is required." });

        if (await _db.ClinicHolidays.AnyAsync(h => h.Date == date))
            return Conflict(new { message = "That date is already marked as a holiday." });

        var holiday = new ClinicHoliday
        {
            Date = date,
            Name = request.Name?.Trim() ?? string.Empty,
        };

        _db.ClinicHolidays.Add(holiday);
        await _db.SaveChangesAsync();

        return Ok(new HolidayDto(holiday.Id, holiday.Date, holiday.Name));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var holiday = await _db.ClinicHolidays.FindAsync(id);
        if (holiday is null) return NotFound();

        _db.ClinicHolidays.Remove(holiday);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
