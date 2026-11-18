namespace Sses.Domain;

public enum EstadoEstrategia
{
    Editable,
    SoloConsulta,
}

/// <summary>CU-01 y CU-02. Una estrategia solo se edita mientras no tenga estudiantes vinculados (RQ_03).</summary>
public class Estrategia
{
    private readonly List<EstrategiaDocente> _docentes = [];
    private readonly List<EstrategiaEstudiante> _estudiantes = [];

    private Estrategia() { }

    public int Id { get; private set; }
    public int EntidadId { get; private set; }
    public int TutorId { get; private set; }
    public string Nombre { get; private set; } = "";
    public string Descripcion { get; private set; } = "";
    public string Periodo { get; private set; } = "";
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }

    /// <summary>Token de concurrencia: dos tutores no pueden pisarse los cambios.</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<EstrategiaDocente> Docentes => _docentes;
    public IReadOnlyCollection<EstrategiaEstudiante> Estudiantes => _estudiantes;

    public bool EsEditable => _estudiantes.Count == 0;
    public EstadoEstrategia Estado => EsEditable ? EstadoEstrategia.Editable : EstadoEstrategia.SoloConsulta;

    public static Estrategia Crear(Entidad entidad, int tutorId, string nombre, string? descripcion, string periodo,
        IEnumerable<Docente> docentes, DateTimeOffset ahora)
    {
        var estrategia = new Estrategia
        {
            EntidadId = entidad.Id,
            TutorId = tutorId,
            CreadaEn = ahora,
        };
        estrategia.Aplicar(nombre, descripcion, periodo, docentes, ahora);
        return estrategia;
    }

    public void Editar(string nombre, string? descripcion, string periodo, IEnumerable<Docente> docentes, DateTimeOffset ahora)
    {
        if (!EsEditable)
            throw new ReglaNegocioException(Errores.EstrategiaBloqueada,
                "Esta estrategia ya tiene estudiantes vinculados y solo está disponible para consulta.");

        Aplicar(nombre, descripcion, periodo, docentes, ahora);
    }

    public EstrategiaEstudiante VincularEstudiante(Estudiante estudiante, DateTimeOffset ahora)
    {
        if (_estudiantes.Any(e => e.EstudianteId == estudiante.Id))
            throw new ReglaNegocioException(Errores.EstudianteYaVinculado,
                $"{estudiante.Nombre} ya está vinculado a esta estrategia.");

        var vinculo = new EstrategiaEstudiante(Id, estudiante.Id, ahora);
        _estudiantes.Add(vinculo);
        ActualizadaEn = ahora;
        return vinculo;
    }

    public bool TieneDocente(int docenteId) => _docentes.Any(d => d.DocenteId == docenteId);

    private void Aplicar(string nombre, string? descripcion, string periodo, IEnumerable<Docente> docentes, DateTimeOffset ahora)
    {
        var lista = docentes.DistinctBy(d => d.Id).ToList();
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre)) faltantes.Add("nombre");
        if (string.IsNullOrWhiteSpace(periodo)) faltantes.Add("periodo");
        if (lista.Count == 0) faltantes.Add("docentes");
        if (faltantes.Count > 0)
            throw new ReglaNegocioException(Errores.DatosIncompletos,
                $"Completa estos datos antes de continuar: {string.Join(", ", faltantes)}.", faltantes);

        if (lista.Any(d => d.EntidadId != EntidadId))
            throw new ReglaNegocioException(Errores.DocenteDeOtraEntidad,
                "Solo puedes asociar docentes registrados en la entidad de la estrategia.", ["docentes"]);

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim() ?? "";
        Periodo = periodo.Trim();
        _docentes.RemoveAll(d => lista.All(n => n.Id != d.DocenteId));
        foreach (var docente in lista.Where(n => _docentes.All(d => d.DocenteId != n.Id)))
            _docentes.Add(new EstrategiaDocente(Id, docente.Id));
        ActualizadaEn = ahora;
    }
}

public class EstrategiaDocente
{
    private EstrategiaDocente() { }

    public EstrategiaDocente(int estrategiaId, int docenteId)
    {
        EstrategiaId = estrategiaId;
        DocenteId = docenteId;
    }

    public int EstrategiaId { get; private set; }
    public int DocenteId { get; private set; }
}

public class EstrategiaEstudiante
{
    private EstrategiaEstudiante() { }

    public EstrategiaEstudiante(int estrategiaId, int estudianteId, DateTimeOffset vinculadoEn)
    {
        EstrategiaId = estrategiaId;
        EstudianteId = estudianteId;
        VinculadoEn = vinculadoEn;
    }

    public int EstrategiaId { get; private set; }
    public int EstudianteId { get; private set; }
    public DateTimeOffset VinculadoEn { get; private set; }
}
