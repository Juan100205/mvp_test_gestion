namespace Sses.Domain;

public enum EstadoSesion
{
    Programada,
    EnCurso,
    Terminada,
}

/// <summary>
/// CU-07: sesión de pilotaje por región. El líder la programa y convoca tutores de esa región; los convocados
/// registran su asistencia al ingresar y, después, responden la encuesta de percepción (RQ_15, RQ_16).
/// </summary>
public class SesionPilotaje
{
    public static readonly TimeSpan IngresoAnticipado = TimeSpan.FromMinutes(30);
    public const int DuracionMinima = 30;
    public const int DuracionMaxima = 240;

    /// <summary>Encuesta de percepción (escala de 1 a 5). El comentario es opcional.</summary>
    public static readonly (string Clave, string Texto)[] Preguntas =
    [
        ("p1", "¿El sistema tiene las funciones que necesitas para tu trabajo?"),
        ("p2", "¿Qué tan fácil fue usar el sistema?"),
        ("p3", "¿Qué tan rápido respondió el sistema?"),
        ("p4", "¿Funcionó bien cuando no tenías señal?"),
        ("p5", "¿Recomendarías el sistema a otros tutores?"),
    ];

    private readonly List<AsistenciaSesion> _asistencias = [];
    private readonly List<IncidenteSesion> _incidentes = [];
    private readonly List<EncuestaPercepcion> _encuestas = [];

    private SesionPilotaje() { }

    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public string Region { get; private set; } = "";
    public DateTimeOffset Inicio { get; private set; }
    public DateTimeOffset Fin { get; private set; }
    public int LiderId { get; private set; }
    public List<int> Convocados { get; private set; } = [];
    public DateTimeOffset CreadaEn { get; private set; }

    public IReadOnlyCollection<AsistenciaSesion> Asistencias => _asistencias;
    public IReadOnlyCollection<IncidenteSesion> Incidentes => _incidentes;
    public IReadOnlyCollection<EncuestaPercepcion> Encuestas => _encuestas;

    public static SesionPilotaje Programar(string nombre, string region, DateTimeOffset inicio, int duracionMinutos,
        IEnumerable<Usuario> tutores, int liderId, DateTimeOffset ahora)
    {
        var lista = tutores.DistinctBy(t => t.Id).ToList();
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre)) faltantes.Add("nombre");
        if (string.IsNullOrWhiteSpace(region)) faltantes.Add("region");
        if (lista.Count == 0) faltantes.Add("tutores");
        if (faltantes.Count > 0)
            throw new ReglaNegocioException(Errores.DatosIncompletos,
                $"Completa estos datos antes de continuar: {string.Join(", ", faltantes)}.", faltantes);

        if (lista.Any(t => t.Rol != Rol.Tutor || t.Region != region.Trim()))
            throw new ReglaNegocioException(Errores.TutorDeOtraRegion,
                $"Solo puedes convocar tutores de la región {region.Trim()}.", ["tutores"]);

        if (inicio < ahora)
            throw new ReglaNegocioException(Errores.HorarioNoValido, "La sesión debe empezar en una fecha y hora futuras.", ["inicio"]);
        if (duracionMinutos is < DuracionMinima or > DuracionMaxima)
            throw new ReglaNegocioException(Errores.HorarioNoValido,
                $"La sesión debe durar entre {DuracionMinima} y {DuracionMaxima} minutos.", ["duracion"]);

        return new SesionPilotaje
        {
            Nombre = nombre.Trim(),
            Region = region.Trim(),
            // PostgreSQL (timestamptz) solo recibe UTC; la hora local se muestra en el cliente.
            Inicio = inicio.ToUniversalTime(),
            Fin = inicio.ToUniversalTime().AddMinutes(duracionMinutos),
            LiderId = liderId,
            Convocados = lista.Select(t => t.Id).Order().ToList(),
            CreadaEn = ahora,
        };
    }

    public EstadoSesion Estado(DateTimeOffset ahora) =>
        ahora < Inicio ? EstadoSesion.Programada : ahora <= Fin ? EstadoSesion.EnCurso : EstadoSesion.Terminada;

    public bool EstaConvocado(int tutorId) => Convocados.Contains(tutorId);
    public bool PermiteIngreso(DateTimeOffset ahora) => ahora >= Inicio - IngresoAnticipado && ahora <= Fin;
    public bool Asistio(int tutorId) => _asistencias.Any(a => a.TutorId == tutorId);
    public bool RespondioEncuesta(int tutorId) => _encuestas.Any(e => e.TutorId == tutorId);

    public AsistenciaSesion RegistrarAsistencia(int tutorId, DateTimeOffset ahora)
    {
        if (!EstaConvocado(tutorId))
            throw new ReglaNegocioException(Errores.NoConvocado, "No estás convocado a esta sesión.");

        var existente = _asistencias.FirstOrDefault(a => a.TutorId == tutorId);
        if (existente is not null) return existente;

        if (!PermiteIngreso(ahora))
            throw new ReglaNegocioException(Errores.SesionFueraDeHorario,
                "Solo puedes ingresar desde 30 minutos antes del inicio y hasta que termine la sesión.");

        var asistencia = new AsistenciaSesion(Id, tutorId, ahora);
        _asistencias.Add(asistencia);
        return asistencia;
    }

    public EncuestaPercepcion ResponderEncuesta(int tutorId, IReadOnlyList<int?> respuestas, string? comentario, DateTimeOffset ahora)
    {
        if (!Asistio(tutorId))
            throw new ReglaNegocioException(Errores.SinAsistencia, "La encuesta es para los tutores que asistieron a la sesión.");
        if (RespondioEncuesta(tutorId))
            throw new ReglaNegocioException(Errores.EncuestaYaRespondida, "Ya respondiste la encuesta de esta sesión.");

        var pendientes = Preguntas.Select((p, i) => (p, valor: i < respuestas.Count ? respuestas[i] : null))
            .Where(x => x.valor is null).ToList();
        if (pendientes.Count > 0)
            throw new ReglaNegocioException(Errores.DatosIncompletos,
                $"Responde estas preguntas antes de guardar: {string.Join(" ", pendientes.Select(x => x.p.Texto))}",
                pendientes.Select(x => x.p.Clave).ToList());

        var fueraDeRango = Preguntas.Select((p, i) => (p, valor: respuestas[i]!.Value)).Where(x => x.valor is < 1 or > 5).ToList();
        if (fueraDeRango.Count > 0)
            throw new ReglaNegocioException(Errores.RespuestaNoValida, "Cada respuesta va de 1 a 5.",
                fueraDeRango.Select(x => x.p.Clave).ToList());

        var encuesta = new EncuestaPercepcion(Id, tutorId, respuestas.Take(Preguntas.Length).Select(r => r!.Value).ToList(),
            comentario?.Trim() ?? "", ahora);
        _encuestas.Add(encuesta);
        return encuesta;
    }

    public IncidenteSesion RegistrarIncidente(string descripcion, DateTimeOffset ahora)
    {
        Guardas.Requeridos(("descripcion", descripcion));
        var incidente = new IncidenteSesion(Id, descripcion.Trim(), ahora);
        _incidentes.Add(incidente);
        return incidente;
    }
}

public class AsistenciaSesion
{
    private AsistenciaSesion() { }

    internal AsistenciaSesion(int sesionId, int tutorId, DateTimeOffset ingresoEn)
    {
        SesionId = sesionId;
        TutorId = tutorId;
        IngresoEn = ingresoEn;
    }

    public int SesionId { get; private set; }
    public int TutorId { get; private set; }
    public DateTimeOffset IngresoEn { get; private set; }
}

public class IncidenteSesion
{
    private IncidenteSesion() { }

    internal IncidenteSesion(int sesionId, string descripcion, DateTimeOffset registradoEn)
    {
        SesionId = sesionId;
        Descripcion = descripcion;
        RegistradoEn = registradoEn;
    }

    public long Id { get; private set; }
    public int SesionId { get; private set; }
    public string Descripcion { get; private set; } = "";
    public DateTimeOffset RegistradoEn { get; private set; }
}

public class EncuestaPercepcion
{
    private EncuestaPercepcion() { }

    internal EncuestaPercepcion(int sesionId, int tutorId, List<int> respuestas, string comentario, DateTimeOffset respondidaEn)
    {
        SesionId = sesionId;
        TutorId = tutorId;
        Respuestas = respuestas;
        Comentario = comentario;
        RespondidaEn = respondidaEn;
    }

    public int SesionId { get; private set; }
    public int TutorId { get; private set; }
    public List<int> Respuestas { get; private set; } = [];
    public string Comentario { get; private set; } = "";
    public DateTimeOffset RespondidaEn { get; private set; }
}

/// <summary>RQ_15 paso 4: uso y tiempo de respuesta de cada petición de un tutor (lo registra un middleware).</summary>
public class MetricaPeticion
{
    private MetricaPeticion() { }

    public MetricaPeticion(int usuarioId, string metodo, string ruta, int estado, int milisegundos, DateTimeOffset fecha)
    {
        UsuarioId = usuarioId;
        Metodo = metodo;
        Ruta = ruta;
        Estado = estado;
        Milisegundos = milisegundos;
        Fecha = fecha;
    }

    public long Id { get; private set; }
    public int UsuarioId { get; private set; }
    public string Metodo { get; private set; } = "";
    public string Ruta { get; private set; } = "";
    public int Estado { get; private set; }
    public int Milisegundos { get; private set; }
    public DateTimeOffset Fecha { get; private set; }
}
