using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProCreateApi.Data;

/// <summary>
/// Design-time only. EF tooling (migrations add / script) builds the context
/// through this, always against Npgsql so the generated migration is Postgres —
/// the runtime provider is chosen from configuration and would otherwise fall
/// back to SQLite here. The connection string is a placeholder; scaffolding a
/// migration does not open a connection.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Must match Program.cs: the app's DateTimes are naive clinic-local
        // values (as under SQLite), so map them to "timestamp without time
        // zone" rather than enforcing UTC. Set before any Npgsql mapping runs.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=procreate;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(options);
    }
}
