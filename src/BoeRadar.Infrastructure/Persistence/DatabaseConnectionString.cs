using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BoeRadar.Infrastructure.Persistence;

public static class DatabaseConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return FromPostgresUrl(databaseUrl);

        return configuration.GetConnectionString("BoeRadar")
            ?? throw new InvalidOperationException(
                "Falta DATABASE_URL o ConnectionStrings:BoeRadar.");
    }

    public static string FromPostgresUrl(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("postgresql" or "postgres") ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            string.IsNullOrWhiteSpace(uri.UserInfo) ||
            uri.AbsolutePath.Length < 2)
        {
            throw new ArgumentException("DATABASE_URL no es una URL PostgreSQL válida.", nameof(databaseUrl));
        }

        var separator = uri.UserInfo.IndexOf(':');
        if (separator < 1)
            throw new ArgumentException("DATABASE_URL debe incluir usuario y contraseña.", nameof(databaseUrl));

        var options = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath[1..]),
            Username = Uri.UnescapeDataString(uri.UserInfo[..separator]),
            Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..])
        };

        return options.ConnectionString;
    }
}
