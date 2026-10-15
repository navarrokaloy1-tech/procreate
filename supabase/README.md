# Supabase (Postgres) for Pro-Create

The API talks to **Supabase Postgres** when a connection string is configured, and
falls back to local **SQLite** when it is not — so day-to-day local dev still runs
offline with no external database.

## How the schema is created

`supabase/migrations/*.sql` is generated from the EF Core model. The Supabase
**GitHub integration** (Dashboard → Integrations → GitHub, project
`mdinkwftkjrhkrdmamfi`) applies any new file in this folder to the database when
you push to the configured branch.

Each script also writes EF's `__EFMigrationsHistory` row, so when the API starts
against the same database its `Database.Migrate()` call sees the migration as
already applied and does nothing. Either path alone creates the schema; together
they do not conflict.

To regenerate after a model change:

```bash
dotnet ef migrations add <Name> --project procreate-api
dotnet ef migrations script --idempotent --project procreate-api \
  -o supabase/migrations/<timestamp>_<name>.sql
```

Use the EF migration's own timestamp as the `<timestamp>` prefix so the file
ordering matches the migration order.

## Pointing the API at Supabase

Set **one** of these (the app also accepts the `postgres://` URI form and
converts it for Npgsql):

- `ConnectionStrings__DefaultConnection`
- `DATABASE_URL`

Get the value from Dashboard → Project Settings → Database → Connection string.
Use the **Session pooler** (port 6543) for serverless/hosted runtimes, or the
direct connection (port 5432) otherwise. TLS is required; the URI path sets
`SSL Mode=Require` automatically.

Example (do **not** commit a real password — use an env var or user-secrets):

```bash
# PowerShell
$env:ConnectionStrings__DefaultConnection = "postgresql://postgres.mdinkwftkjrhkrdmamfi:<password>@aws-0-<region>.pooler.supabase.com:6543/postgres"
dotnet run --project procreate-api --launch-profile http
```

With no connection string set, the API uses `procreate.db` (SQLite) as before.
