using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Services.Appointments;

/// <summary>Why a block could not take the booking, or null when it can.</summary>
public record BatchRefusal(string Message, bool IsConflict);

/// <summary>
/// The rules for putting a patient into a session block.
///
/// Shared rather than sitting in one controller: the front desk books through
/// the appointments endpoint and a patient books through the portal, and the
/// two must agree on what a full block is. Duplicated, they would drift, and
/// the copy that drifted would quietly overfill a clinic session.
/// </summary>
public class BatchBooking
{
    private readonly AppDbContext _db;

    public BatchBooking(AppDbContext db) => _db = db;

    /// <summary>Statuses that no longer occupy a seat.</summary>
    public static readonly string[] ReleasedStatuses = { "Cancelled", "NoShow" };

    /// <summary>"09:00" as a TimeSpan; falls back to midnight on anything odd.</summary>
    public static TimeSpan ParseTime(string value) =>
        TimeSpan.TryParse(value, out var parsed) ? parsed : TimeSpan.Zero;

    /// <summary>"13:00" as "1:00 PM", for labels and messages.</summary>
    public static string Display(string value) =>
        TimeSpan.TryParse(value, out var parsed)
            ? DateTime.Today.Add(parsed).ToString("h:mm tt")
            : value;

    public static string Label(AppointmentBatch batch) =>
        $"{Display(batch.StartTime)}–{Display(batch.EndTime)}";

    /// <summary>Appointments in a block that still hold a seat.</summary>
    public static IEnumerable<Appointment> Occupants(
        AppointmentBatch batch, int? excludeAppointmentId = null) =>
        batch.Appointments
            .Where(a => a.Id != excludeAppointmentId && !a.IsArchived)
            .Where(a => !ReleasedStatuses.Contains(a.Status));

    /// <summary>Loads a block with the appointments already in it.</summary>
    public Task<AppointmentBatch?> FindAsync(int batchId) =>
        _db.AppointmentBatches
            .Include(b => b.Appointments)
            .FirstOrDefaultAsync(b => b.Id == batchId);

    /// <summary>
    /// Whether this patient can be placed in this block. Pass the appointment
    /// being edited so moving one within its own block does not collide
    /// with itself.
    /// </summary>
    public static BatchRefusal? Check(
        AppointmentBatch batch, int patientId, int? excludeAppointmentId = null)
    {
        var label = Label(batch);

        if (batch.IsClosed)
            return new BatchRefusal($"The {label} block is closed for bookings.", true);

        var occupants = Occupants(batch, excludeAppointmentId).ToList();

        if (occupants.Count >= batch.Capacity)
            return new BatchRefusal(
                $"The {label} block is full — it takes {batch.Capacity} patients.", true);

        if (occupants.Any(a => a.PatientId == patientId))
            return new BatchRefusal($"That patient is already booked into the {label} block.", true);

        return null;
    }

    /// <summary>The wall-clock time a block starts on its own date.</summary>
    public static DateTime StartsAt(AppointmentBatch batch) =>
        batch.BatchDate.Add(ParseTime(batch.StartTime));

    /// <summary>Next free place at the back of a block's queue.</summary>
    public static int NextPosition(AppointmentBatch batch) =>
        batch.Appointments.Count == 0 ? 1 : batch.Appointments.Max(a => a.QueuePosition) + 1;

    /// <summary>
    /// Copies the weekday template onto a date, once. Existing blocks are left
    /// alone, so a day adjusted by hand is never reset.
    /// </summary>
    public async Task EnsureBatchesForAsync(DateTime day)
    {
        if (await _db.AppointmentBatches.AnyAsync(b => b.BatchDate == day)) return;

        var templates = await _db.AppointmentBatchTemplates
            .Where(t => t.DayOfWeek == (int)day.DayOfWeek && t.IsActive)
            .OrderBy(t => t.StartTime)
            .ToListAsync();

        if (templates.Count == 0) return;

        _db.AppointmentBatches.AddRange(templates.Select(t => new AppointmentBatch
        {
            BatchDate = day,
            StartTime = t.StartTime,
            EndTime = t.EndTime,
            Capacity = t.Capacity,
            TemplateId = t.Id
        }));

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another request materialised the same day first; the unique index
            // on (BatchDate, StartTime) caught it. Its rows are just as good.
            foreach (var entry in _db.ChangeTracker.Entries<AppointmentBatch>().ToList())
                entry.State = EntityState.Detached;
        }
    }
}
