namespace Sses.Domain;

public class Usuario
{
    public const int MaxIntentos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    private Usuario() { }

    public Usuario(int id, string username, string passwordHash, string nombre, Rol rol, string region)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Nombre = nombre;
        Rol = rol;
        Region = region;
    }

    public int Id { get; private set; }
    public string Username { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public Rol Rol { get; private set; }
    public string Region { get; private set; } = "";
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }
    public DateTimeOffset? UltimoIngreso { get; private set; }

    public int IntentosRestantes => Math.Max(0, MaxIntentos - IntentosFallidos);

    public bool EstaBloqueado(DateTimeOffset ahora) => BloqueadoHasta is { } hasta && hasta > ahora;

    public void RegistrarIntentoFallido(DateTimeOffset ahora)
    {
        if (BloqueadoHasta is { } hasta && hasta <= ahora)
        {
            IntentosFallidos = 0;
            BloqueadoHasta = null;
        }

        IntentosFallidos++;
        if (IntentosFallidos >= MaxIntentos)
            BloqueadoHasta = ahora.Add(DuracionBloqueo);
    }

    public void RegistrarIngresoExitoso(DateTimeOffset ahora)
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        UltimoIngreso = ahora;
    }
}
