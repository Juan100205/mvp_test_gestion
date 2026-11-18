namespace Sses.Domain;

// Información precargada del piloto: el SSES la consulta y la asocia, pero no la crea.

public class Entidad
{
    private Entidad() { }

    public Entidad(int id, string nombre, string region, string municipio)
    {
        Id = id;
        Nombre = nombre;
        Region = region;
        Municipio = municipio;
    }

    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public string Region { get; private set; } = "";
    public string Municipio { get; private set; } = "";
}

public class Docente
{
    private Docente() { }

    public Docente(int id, string documento, string nombre, int entidadId)
    {
        Id = id;
        Documento = documento;
        Nombre = nombre;
        EntidadId = entidadId;
    }

    public int Id { get; private set; }
    public string Documento { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public int EntidadId { get; private set; }
}

public class Estudiante
{
    private Estudiante() { }

    public Estudiante(int id, string tipoDocumento, string numeroDocumento, string nombre, int entidadId)
    {
        Id = id;
        TipoDocumento = tipoDocumento;
        NumeroDocumento = numeroDocumento;
        Nombre = nombre;
        EntidadId = entidadId;
    }

    public int Id { get; private set; }
    public string TipoDocumento { get; private set; } = "";
    public string NumeroDocumento { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public int EntidadId { get; private set; }
}

/// <summary>RQ_14: registro automático de quién hizo qué y cuándo.</summary>
public class RegistroAuditoria
{
    private RegistroAuditoria() { }

    public RegistroAuditoria(int? usuarioId, string usuario, string operacion, string recurso, string? recursoId, string? detalle, DateTimeOffset fecha)
    {
        UsuarioId = usuarioId;
        Usuario = usuario;
        Operacion = operacion;
        Recurso = recurso;
        RecursoId = recursoId;
        Detalle = detalle;
        Fecha = fecha;
    }

    public long Id { get; private set; }
    public int? UsuarioId { get; private set; }
    public string Usuario { get; private set; } = "";
    public string Operacion { get; private set; } = "";
    public string Recurso { get; private set; } = "";
    public string? RecursoId { get; private set; }
    public string? Detalle { get; private set; }
    public DateTimeOffset Fecha { get; private set; }
}
