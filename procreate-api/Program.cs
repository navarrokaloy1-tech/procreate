using ProCreateApi.Data;
using ProCreateApi.Services.Appointments;
using ProCreateApi.Services.Auth;
using ProCreateApi.Services.Clinic;
using ProCreateApi.Services.Inventory;
using ProCreateApi.Services.Pdf;
using ProCreateApi.Services.Email;
using ProCreateApi.Services.Lis;
using ProCreateApi.Services.Locations;
using ProCreateApi.Services.Queue;
using ProCreateApi.Services.Sso;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// The app's DateTimes are naive clinic-local values (as they were under SQLite),
// so keep Npgsql mapping them to "timestamp without time zone" instead of
// enforcing UTC-kind. Must run before any Npgsql type mapping is built, and must
// match AppDbContextFactory so the migration and runtime agree on the type.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// LIS integration services
var lisSettings = builder.Configuration.GetSection("Lis").Get<LisSettings>() ?? new LisSettings();
builder.Services.AddSingleton(lisSettings);
builder.Services.AddSingleton<Hl7Builder>();
builder.Services.AddSingleton<MllpClient>();
builder.Services.AddScoped<ILisService, LisService>();
builder.Services.AddHostedService<MllpServer>();

// Address reference data (PSGC) with in-memory caching and an offline fallback.
var psgcOptions = builder.Configuration.GetSection("Psgc").Get<PsgcOptions>() ?? new PsgcOptions();
builder.Services.AddSingleton(psgcOptions);
builder.Services.AddSingleton<PsgcStatus>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ILocationService, PsgcLocationService>(client =>
{
    // Trailing slash matters: relative paths are resolved against it.
    client.BaseAddress = new Uri(psgcOptions.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(psgcOptions.TimeoutSeconds + 2);
});

// Outgoing result emails. Blank settings are valid: sending is refused with a
// clear message rather than silently doing nothing.
var emailSettings = builder.Configuration.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
builder.Services.AddSingleton(emailSettings);
builder.Services.AddScoped<IEmailSender, EmailSender>();

// Shared by the front-desk and patient booking paths so both enforce the
// same idea of a full block.
builder.Services.AddScoped<BatchBooking>();

builder.Services.AddScoped<QueueAllocator>();

// Clinic letterhead details for result sheets (printed, previewed, emailed).
var clinicSettings = builder.Configuration.GetSection("Clinic").Get<ClinicSettings>() ?? new ClinicSettings();
builder.Services.AddSingleton(clinicSettings);

// Renders result sheets to PDF for emailed results.
builder.Services.AddSingleton<ResultSheetPdf>();

// Single sign-on against an OpenID Connect provider (Authentik in front of
// Google, in this clinic). Blank settings are valid: SSO reports itself
// disabled and the sign-in screens fall back to a password.
var ssoSettings = builder.Configuration.GetSection("Sso").Get<SsoSettings>() ?? new SsoSettings();
builder.Services.AddSingleton(ssoSettings);
builder.Services.AddSingleton<OidcDiscovery>();
builder.Services.AddSingleton<SsoHandoff>();
builder.Services.AddHttpClient<OidcClient>(client => client.Timeout = TimeSpan.FromSeconds(20));

// Every sign-in route mints its session here, password or SSO alike.
builder.Services.AddScoped<TokenIssuer>();

// Records sign-in attempts (success and failure) for the login activity trail.
builder.Services.AddScoped<LoginAudit>();

// The single chokepoint for stock movements (stock-in/out, corrections, auto-deduction).
builder.Services.AddScoped<InventoryLedger>();

// Inventory alerts: the shared report feeds the endpoint, the header bell and
// the email digest. The digest is scheduled but ships disabled — see
// InventoryAlertSettings — so nothing is emailed until the clinic turns it on.
builder.Services.AddScoped<InventoryAlertService>();
var inventoryAlertSettings =
    builder.Configuration.GetSection("InventoryAlerts").Get<InventoryAlertSettings>() ?? new InventoryAlertSettings();
builder.Services.AddSingleton(inventoryAlertSettings);
builder.Services.AddScoped<InventoryAlertDigest>();
builder.Services.AddHostedService<InventoryAlertScheduler>();

// Postgres (Supabase) when a connection string is configured; SQLite otherwise,
// so local dev still runs offline with no external database. Supabase's string
// is accepted in either the postgres:// URI form or Npgsql key=value form.
var pgConnectionRaw =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");
var usePostgres = !string.IsNullOrWhiteSpace(pgConnectionRaw);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (usePostgres)
        opt.UseNpgsql(DbConnectionString.ForNpgsql(pgConnectionRaw!))
            // The seeded users hash their passwords with BCrypt inside HasData,
            // which produces a fresh value every build, so the model never matches
            // the migration snapshot. That is harmless here — the schema is fixed
            // and the hashes still verify — but EF 10 escalates it to an error that
            // aborts Migrate(). Ignore just that warning so startup can proceed.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    else
        opt.UseSqlite("Data Source=procreate.db");
});
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        opt.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Lock the API down by default: every endpoint requires an authenticated user
// unless it opts out with [AllowAnonymous]. Previously only the patient portal
// enforced auth server-side and every staff endpoint was reachable without a
// token — role-gating was UI-only. The fallback policy closes that hole; the
// genuinely public endpoints (sign-in, SSO, PSGC lookups, kiosk self-register,
// the queue display board) are marked [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200", "http://192.168.254.103:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (usePostgres)
    {
        // Postgres (Supabase) owns its schema through EF migrations. The same
        // migration is also shipped as SQL under supabase/migrations, which the
        // Supabase GitHub integration applies on push; because that script writes
        // the __EFMigrationsHistory row, Migrate() here sees it as already done
        // and is a no-op. Either path alone creates the database.
        db.Database.Migrate();
    }
    else
    {
        // Local SQLite dev keeps the lightweight EnsureCreated path. EnsureCreated
        // leaves an existing database untouched, so columns added after it was
        // first created have to be put in by hand.
        db.Database.EnsureCreated();

        var labTestColumns = db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('LabTests')")
            .ToList();

        if (!labTestColumns.Contains("ProductId"))
            db.Database.ExecuteSqlRaw("""ALTER TABLE "LabTests" ADD COLUMN "ProductId" INTEGER NULL""");
        if (!labTestColumns.Contains("IsActive"))
            db.Database.ExecuteSqlRaw("""ALTER TABLE "LabTests" ADD COLUMN "IsActive" INTEGER NOT NULL DEFAULT 1""");

        db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_LabTests_ProductId" ON "LabTests" ("ProductId") WHERE "ProductId" IS NOT NULL""");
        // Parameters moved onto LabTest before this table ever held anything.
        db.Database.ExecuteSqlRaw("""DROP TABLE IF EXISTS "ServiceParameters" """);
    }

    DataSeeder.SeedSampleData(db);
    CatalogueReconciler.Reconcile(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
