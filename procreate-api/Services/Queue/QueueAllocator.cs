using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Services.Queue;

/// <summary>
/// Issues reception queue tickets.
///
/// Shared so the front desk and the self-registration kiosk allocate numbers
/// the same way. Numbers restart each day, and (QueueDate, QueueNumber) is
/// unique, so allocation retries on conflict rather than handing two people
/// the same ticket.
/// </summary>
public class QueueAllocator
{
    private const int MaxAttempts = 5;

    private readonly AppDbContext _db;

    public QueueAllocator(AppDbContext db) { _db = db; }

    /// <summary>
    /// Adds an entry to today's queue. Returns null if a number could not be
    /// allocated after retrying.
    /// </summary>
    public async Task<QueueEntry?> EnqueueAsync(string patientName, int? patientId)
    {
        var day = DateTime.Today;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var lastNumber = await _db.QueueEntries
                .Where(q => q.QueueDate == day)
                .MaxAsync(q => (int?)q.QueueNumber) ?? 0;

            var entry = new QueueEntry
            {
                QueueNumber = lastNumber + 1,
                QueueDate = day,
                PatientName = patientName.Trim(),
                PatientId = patientId,
                Status = "Waiting",
                AddedAt = DateTime.Now
            };

            _db.QueueEntries.Add(entry);

            try
            {
                await _db.SaveChangesAsync();
                return entry;
            }
            catch (DbUpdateException)
            {
                // Someone took that number between the read and the write.
                _db.Entry(entry).State = EntityState.Detached;
            }
        }

        return null;
    }
}
