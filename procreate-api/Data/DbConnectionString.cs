using Npgsql;

namespace ProCreateApi.Data;

/// <summary>
/// Accepts the connection string in whichever shape it arrives. Supabase hands
/// out a URI ("postgresql://user:pass@host:6543/postgres"), while Npgsql wants
/// a key=value string — so a URI is translated and anything else is passed
/// through untouched.
/// </summary>
public static class DbConnectionString
{
    public static string ForNpgsql(string raw)
    {
        raw = raw.Trim();

        var isUri =
            raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        if (!isUri)
            return raw;

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            // Supabase requires TLS. A key=value string the caller supplies
            // directly can set its own SslMode; this only covers the URI path.
            SslMode = SslMode.Require,
        };

        // Honour an explicit ?sslmode= in the URI if one is present.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(kv[1], true, out var mode))
            {
                builder.SslMode = mode;
            }
        }

        return builder.ConnectionString;
    }
}
