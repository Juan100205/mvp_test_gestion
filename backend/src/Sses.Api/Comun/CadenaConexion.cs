using Npgsql;

namespace Sses.Api.Comun;

/// <summary>
/// Acepta la cadena de Npgsql (Host=...;) o la URL que entregan los proveedores
/// administrados (postgres://usuario:clave@host:puerto/base).
/// </summary>
public static class CadenaConexion
{
    public static string Normalizar(string cadena)
    {
        if (!cadena.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !cadena.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return cadena;

        var uri = new Uri(cadena);
        var credenciales = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credenciales[0]),
            Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : null,
        };

        // Respeta ?sslmode=require (Neon, Supabase, URL externa de Render).
        foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = par.Split('=', 2);
            if (partes.Length == 2 && partes[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(partes[1], ignoreCase: true, out var modo))
                builder.SslMode = modo;
        }

        return builder.ConnectionString;
    }
}
