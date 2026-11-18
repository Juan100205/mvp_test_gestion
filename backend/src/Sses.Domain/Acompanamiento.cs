namespace Sses.Domain;

public enum TipoAcompanamiento
{
    Individual,
    Grupal,
}

/// <summary>
/// CU-03. Acompañamiento pedagógico mensual (individual o grupal) con seguimiento semanal.
/// Los Id son Guid generados en el cliente para que los registros hechos sin conexión
/// se sincronicen sin duplicarse (RQ_21).
/// </summary>
public class Acompanamiento
{
    public const int SemanasPorMes = 5;
    private readonly List<Seguimiento> _seguimientos = [];

    private Acompanamiento() { }

    public Guid Id { get; private set; }
    public int EstrategiaId { get; private set; }
    public int TutorId { get; private set; }
    public TipoAcompanamiento Tipo { get; private set; }
    public DateOnly Periodo { get; private set; }
    public string Objetivo { get; private set; } = "";
    public string Compromisos { get; private set; } = "";
    public List<int> DocenteIds { get; private set; } = [];

    /// <summary>Identifica al docente o al grupo: un acompañamiento por grupo y por mes (flujo alterno 4A).</summary>
    public string ClaveGrupo { get; private set; } = "";

    public DateTimeOffset RegistradoEn { get; private set; }

    public IReadOnlyCollection<Seguimiento> Seguimientos => _seguimientos;

    public int AvanceActual => _seguimientos.OrderByDescending(s => s.Semana).Select(s => s.Avance).FirstOrDefault();

    public static string CalcularClave(IEnumerable<int> docenteIds) =>
        string.Join('-', docenteIds.Distinct().Order());

    public static DateOnly NormalizarPeriodo(DateOnly fecha) => new(fecha.Year, fecha.Month, 1);

    public static Acompanamiento Registrar(Guid id, Estrategia estrategia, TipoAcompanamiento tipo, DateOnly periodo,
        string objetivo, string? compromisos, IEnumerable<int> docenteIds, int tutorId, DateTimeOffset ahora)
    {
        Guardas.Requeridos(("objetivo", objetivo));

        var docentes = docenteIds.Distinct().Order().ToList();
        var valido = tipo == TipoAcompanamiento.Individual ? docentes.Count == 1 : docentes.Count >= 2;
        if (!valido)
            throw new ReglaNegocioException(Errores.DocentesNoCorrespondenAlTipo,
                tipo == TipoAcompanamiento.Individual
                    ? "Un acompañamiento individual es con un solo docente."
                    : "Un acompañamiento grupal necesita al menos dos docentes.", ["docentes"]);

        if (docentes.Any(d => !estrategia.TieneDocente(d)))
            throw new ReglaNegocioException(Errores.DocenteNoAsociado,
                "Solo puedes acompañar a docentes asociados a esta estrategia.", ["docentes"]);

        return new Acompanamiento
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            EstrategiaId = estrategia.Id,
            TutorId = tutorId,
            Tipo = tipo,
            Periodo = NormalizarPeriodo(periodo),
            Objetivo = objetivo.Trim(),
            Compromisos = compromisos?.Trim() ?? "",
            DocenteIds = docentes,
            ClaveGrupo = CalcularClave(docentes),
            RegistradoEn = ahora,
        };
    }

    /// <summary>Registra el seguimiento de la semana; si la semana ya existe lo actualiza (flujo alterno 5A).</summary>
    public Seguimiento RegistrarSeguimiento(Guid id, int semana, string actividades, int avance, string? observaciones, DateTimeOffset ahora)
    {
        if (semana is < 1 or > SemanasPorMes)
            throw new ReglaNegocioException(Errores.SeguimientoInvalido, $"La semana debe estar entre 1 y {SemanasPorMes}.", ["semana"]);
        if (avance is < 0 or > 100)
            throw new ReglaNegocioException(Errores.SeguimientoInvalido, "El avance debe estar entre 0 y 100 %.", ["avance"]);
        Guardas.Requeridos(("actividades", actividades));

        var existente = _seguimientos.FirstOrDefault(s => s.Semana == semana);
        if (existente is not null)
        {
            existente.Actualizar(actividades, avance, observaciones, ahora);
            return existente;
        }

        var nuevo = new Seguimiento(id == Guid.Empty ? Guid.NewGuid() : id, Id, semana, actividades, avance, observaciones, ahora);
        _seguimientos.Add(nuevo);
        return nuevo;
    }
}

public class Seguimiento
{
    private Seguimiento() { }

    internal Seguimiento(Guid id, Guid acompanamientoId, int semana, string actividades, int avance, string? observaciones, DateTimeOffset ahora)
    {
        Id = id;
        AcompanamientoId = acompanamientoId;
        Semana = semana;
        RegistradoEn = ahora;
        Actualizar(actividades, avance, observaciones, ahora);
    }

    public Guid Id { get; private set; }
    public Guid AcompanamientoId { get; private set; }
    public int Semana { get; private set; }
    public string Actividades { get; private set; } = "";
    public int Avance { get; private set; }
    public string Observaciones { get; private set; } = "";
    public DateTimeOffset RegistradoEn { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    internal void Actualizar(string actividades, int avance, string? observaciones, DateTimeOffset ahora)
    {
        Actividades = actividades.Trim();
        Avance = avance;
        Observaciones = observaciones?.Trim() ?? "";
        ActualizadoEn = ahora;
    }
}
