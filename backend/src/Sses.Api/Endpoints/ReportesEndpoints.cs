using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sses.Api.Comun;
using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;
using F = Sses.Api.Comun.Formato;

namespace Sses.Api.Endpoints;

/// <summary>
/// CU-04 (desempeño, actividades y reportes de ley) y CU-05 (reportes para el ente de control).
/// Solo lectura. Cada reporte es un documento genérico con cifras y tablas; ninguno expone datos personales de estudiantes.
/// </summary>
public static partial class ReportesEndpoints
{
    private static readonly string[] Formatos = ["csv", "pdf"];
    private static readonly Rol[] Internos = [Rol.Funcionario, Rol.Administrador];
    private const string NotaAgregados = "Cifras agregadas, sin datos personales de estudiantes (Ley 1581 de 2012).";

    private static readonly Dictionary<string, string> NombreFiltro = new()
    {
        ["region"] = "región",
        ["entidadId"] = "entidad",
        ["tutorId"] = "tutor",
        ["periodo"] = "periodo académico",
        ["mes"] = "mes del acompañamiento",
    };

    internal sealed record TipoReporte(string Id, string Nombre, string Descripcion, string Grupo, Rol[] Roles, string[] Filtros,
        string[] Requeridos);

    /// <summary>Reportes autorizados por perfil (CU-04 y CU-05, paso 2).</summary>
    internal static readonly TipoReporte[] Tipos =
    [
        new("consolidado", "Estrategias y acompañamiento",
            "Estrategias por entidad con estudiantes vinculados, acompañamientos, seguimientos y avance.", "Control",
            [Rol.EnteControl, .. Internos], ["region", "entidadId", "periodo", "mes"], []),
        new("desempeno", "Desempeño de tutores",
            "Indicadores por tutor: estrategias, estudiantes, acompañamientos, seguimientos y avance.", "Desempeño",
            Internos, ["region", "entidadId", "tutorId", "periodo", "mes"], []),
        new("actividades", "Actividades realizadas",
            "Cada acompañamiento con sus docentes, seguimientos semanales y último avance registrado.", "Desempeño",
            Internos, ["region", "entidadId", "tutorId", "periodo", "mes"], []),
        new("ley-cobertura", "Cobertura del programa",
            "Reporte de ley: estudiantes y docentes atendidos por entidad y región en el periodo, con fecha de corte.", "Reportes de ley",
            Internos, ["region", "entidadId", "periodo"], ["periodo"]),
        new("ley-datos-personales", "Tratamiento de datos personales",
            "Reporte de ley (Ley 1581 de 2012): datos personales tratados, titulares, medidas de protección y accesos registrados.",
            "Reportes de ley", Internos, ["periodo"], []),
    ];

    public static void MapReportes(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reportes").WithTags("Reportes").RequireAuthorization(Politicas.Reportes);
        g.MapGet("/tipos", ListarTipos);
        g.MapGet("/filtros", Filtros);
        g.MapGet("/{tipo}", Generar);
        g.MapPost("/descargas", RegistrarDescarga);
    }

    private static IResult ListarTipos(ClaimsPrincipal user) =>
        Results.Ok(Tipos.Where(t => t.Roles.Contains(user.RolUsuario()))
            .Select(t => new TipoReporteDto(t.Id, t.Nombre, t.Descripcion, t.Grupo, t.Filtros, t.Requeridos)));

    private static async Task<IResult> Filtros(SsesDbContext db, CancellationToken ct)
    {
        var entidades = await db.Entidades.AsNoTracking().OrderBy(e => e.Region).ThenBy(e => e.Nombre)
            .Select(e => new EntidadDto(e.Id, e.Nombre, e.Region, e.Municipio)).ToListAsync(ct);
        var tutores = await db.Usuarios.AsNoTracking().Where(u => u.Rol == Rol.Tutor).OrderBy(u => u.Nombre)
            .Select(u => new TutorReporteDto(u.Id, u.Nombre, u.Region)).ToListAsync(ct);
        var periodos = await db.Estrategias.AsNoTracking().Select(e => e.Periodo).Distinct().OrderByDescending(p => p).ToListAsync(ct);
        var meses = (await db.Acompanamientos.AsNoTracking().Select(a => a.Periodo).Distinct().OrderByDescending(p => p).ToListAsync(ct))
            .Select(Mapeos.FormatoPeriodo).ToList();

        return Results.Ok(new FiltrosReporteDto(entidades.Select(e => e.Region).Distinct().Order().ToList(), entidades, tutores, periodos, meses));
    }

    private static async Task<IResult> Generar(string tipo, ClaimsPrincipal user, SsesDbContext db, Auditor auditor, TimeProvider reloj,
        string? region, int? entidadId, int? tutorId, string? periodo, string? mes, CancellationToken ct)
    {
        var definicion = Tipos.FirstOrDefault(t => t.Id == tipo);
        if (definicion is null) return Problema.NoEncontrado("reporte_no_encontrado", "Ese reporte no existe.");

        var (filtros, error) = await Validar(definicion, user.RolUsuario(), db, region, entidadId, tutorId, periodo, mes, ct);
        if (error is not null) return error;

        var ahora = reloj.GetUtcNow();
        var documento = tipo switch
        {
            "consolidado" => await Consolidado(definicion, filtros!, db, ahora, ct),
            "desempeno" => await Desempeno(definicion, filtros!, db, ahora, ct),
            "actividades" => await Actividades(definicion, filtros!, db, ahora, ct),
            "ley-cobertura" => await Cobertura(definicion, filtros!, db, ahora, ct),
            _ => await DatosPersonales(definicion, filtros!, db, ahora, ct),
        };

        auditor.Registrar("reporte.generar", "reporte", tipo, filtros!.Descripcion);
        await db.SaveChangesAsync(ct);
        return Results.Ok(documento);
    }

    private static async Task<IResult> RegistrarDescarga(DescargaReporteRequest req, ClaimsPrincipal user, Auditor auditor, SsesDbContext db,
        CancellationToken ct)
    {
        var definicion = Tipos.FirstOrDefault(t => t.Id == req.Tipo);
        if (definicion is null || !definicion.Roles.Contains(user.RolUsuario()))
            return NoAutorizado(user.RolUsuario(), []);

        var formato = req.Formato?.Trim().ToLowerInvariant() ?? "";
        if (!Formatos.Contains(formato))
            return Problema.Crear(StatusCodes.Status400BadRequest, "formato_no_valido", "El reporte se descarga en CSV o PDF.");

        var filtros = req.Filtros?.Trim() ?? "";
        auditor.Registrar("reporte.descargar", "reporte", $"{definicion.Id}.{formato}", filtros.Length == 0 ? "Sin filtros" : F.Recortar(filtros, 500));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---------- Validación de criterios (CU-05 3A y CU-04 3A) ----------

    internal sealed record Criterios(string? Region, Entidad? Entidad, Usuario? Tutor, string? Periodo, DateOnly? Mes)
    {
        public string Descripcion
        {
            get
            {
                var partes = new List<string>();
                if (Region is not null) partes.Add($"Región: {Region}");
                if (Entidad is not null) partes.Add($"Entidad: {Entidad.Nombre}");
                if (Tutor is not null) partes.Add($"Tutor: {Tutor.Nombre}");
                if (Periodo is not null) partes.Add($"Periodo: {Periodo}");
                if (Mes is not null) partes.Add($"Mes: {F.Mes(Mes.Value)}");
                return partes.Count == 0 ? "Sin filtros" : string.Join(" · ", partes);
            }
        }
    }

    private static IResult NoAutorizado(Rol rol, IReadOnlyList<string> campos, TipoReporte? tipo = null)
    {
        var autorizados = Tipos.Where(t => t.Roles.Contains(rol)).ToList();
        var detalle = tipo is null
            ? $"Tu perfil puede consultar: {string.Join(", ", autorizados.Select(t => t.Nombre))}."
            : $"Con «{tipo.Nombre}» puedes filtrar por {string.Join(", ", tipo.Filtros.Select(f => NombreFiltro[f]))}.";
        return Problema.Crear(StatusCodes.Status403Forbidden, "criterio_no_autorizado", detalle,
            new Dictionary<string, object?> { ["campos"] = campos, ["autorizados"] = autorizados.Select(t => t.Id).ToList() });
    }

    private static async Task<(Criterios?, IResult?)> Validar(TipoReporte tipo, Rol rol, SsesDbContext db, string? region, int? entidadId,
        int? tutorId, string? periodo, string? mes, CancellationToken ct)
    {
        if (!tipo.Roles.Contains(rol)) return (null, NoAutorizado(rol, []));

        var recibidos = new (string Campo, bool Presente)[]
        {
            ("region", !string.IsNullOrWhiteSpace(region)), ("entidadId", entidadId is not null), ("tutorId", tutorId is not null),
            ("periodo", !string.IsNullOrWhiteSpace(periodo)), ("mes", !string.IsNullOrWhiteSpace(mes)),
        };
        var fueraDePermiso = recibidos.Where(r => r.Presente && !tipo.Filtros.Contains(r.Campo)).Select(r => r.Campo).ToList();
        if (fueraDePermiso.Count > 0) return (null, NoAutorizado(rol, fueraDePermiso, tipo));

        var errores = new List<(string Campo, string Mensaje)>();
        region = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        periodo = string.IsNullOrWhiteSpace(periodo) ? null : periodo.Trim();

        foreach (var requerido in tipo.Requeridos.Where(r => !recibidos.Single(x => x.Campo == r).Presente))
            errores.Add((requerido, $"Elige el {NombreFiltro[requerido]}: este reporte se genera por {NombreFiltro[requerido]}."));

        if (periodo is not null && !PeriodoValido().IsMatch(periodo))
            errores.Add(("periodo", "El periodo académico va en formato AAAA-1 o AAAA-2."));

        DateOnly? mesValido = null;
        if (!string.IsNullOrWhiteSpace(mes))
        {
            if (DateOnly.TryParseExact($"{mes.Trim()}-01", "yyyy-MM-dd", out var m)) mesValido = m;
            else errores.Add(("mes", "El mes va en formato AAAA-MM."));
        }

        Entidad? entidad = null;
        if (entidadId is not null)
        {
            entidad = await db.Entidades.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entidadId, ct);
            if (entidad is null || (region is not null && entidad.Region != region))
                errores.Add(("entidadId", "La entidad no existe o no pertenece a la región elegida."));
        }

        Usuario? tutor = null;
        if (tutorId is not null)
        {
            tutor = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == tutorId && u.Rol == Rol.Tutor, ct);
            if (tutor is null) errores.Add(("tutorId", "Ese tutor no existe."));
        }

        if (errores.Count > 0)
            return (null, Problema.Crear(StatusCodes.Status400BadRequest, "filtros_no_validos",
                $"Corrige estos filtros: {string.Join(" ", errores.Select(e => e.Mensaje))}",
                new Dictionary<string, object?> { ["campos"] = errores.Select(e => e.Campo).Distinct().ToList() }));

        return (new Criterios(region, entidad, tutor, periodo, mesValido), null);
    }

    [GeneratedRegex(@"^\d{4}-[12]$")]
    private static partial Regex PeriodoValido();

    // ---------- Consultas base ----------

    private sealed class FilaEstrategia
    {
        public int Id { get; init; }
        public string Nombre { get; init; } = "";
        public string Periodo { get; init; } = "";
        public int EntidadId { get; init; }
        public string Entidad { get; init; } = "";
        public string Region { get; init; } = "";
        public int TutorId { get; init; }
        public string Tutor { get; init; } = "";
        public string TutorRegion { get; init; } = "";
        public int Estudiantes { get; init; }
        public int Docentes { get; init; }
    }

    private sealed class FilaAcompanamiento
    {
        public Guid Id { get; init; }
        public int EstrategiaId { get; init; }
        public TipoAcompanamiento Tipo { get; init; }
        public DateOnly Periodo { get; init; }
        public List<int> DocenteIds { get; init; } = [];
        public int Seguimientos { get; init; }
        public int Avance { get; init; }
        public int UltimaSemana { get; init; }
        public string UltimasActividades { get; init; } = "";
        public DateTimeOffset? UltimaActualizacion { get; init; }
    }

    private static Task<List<FilaEstrategia>> Estrategias(SsesDbContext db, Criterios f, CancellationToken ct)
    {
        var q = from e in db.Estrategias.AsNoTracking()
                join en in db.Entidades on e.EntidadId equals en.Id
                join t in db.Usuarios on e.TutorId equals t.Id
                select new { e, en, t };
        if (f.Region is not null) q = q.Where(x => x.en.Region == f.Region);
        if (f.Entidad is not null) q = q.Where(x => x.en.Id == f.Entidad.Id);
        if (f.Tutor is not null) q = q.Where(x => x.t.Id == f.Tutor.Id);
        if (f.Periodo is not null) q = q.Where(x => x.e.Periodo == f.Periodo);

        return q.OrderBy(x => x.en.Region).ThenBy(x => x.en.Nombre).ThenBy(x => x.e.Nombre)
            .Select(x => new FilaEstrategia
            {
                Id = x.e.Id,
                Nombre = x.e.Nombre,
                Periodo = x.e.Periodo,
                EntidadId = x.en.Id,
                Entidad = x.en.Nombre,
                Region = x.en.Region,
                TutorId = x.t.Id,
                Tutor = x.t.Nombre,
                TutorRegion = x.t.Region,
                Estudiantes = x.e.Estudiantes.Count,
                Docentes = x.e.Docentes.Count,
            })
            .ToListAsync(ct);
    }

    private static Task<List<FilaAcompanamiento>> Acompanamientos(SsesDbContext db, IReadOnlyCollection<int> estrategiaIds, DateOnly? mes,
        CancellationToken ct)
    {
        var q = db.Acompanamientos.AsNoTracking().Where(a => estrategiaIds.Contains(a.EstrategiaId));
        if (mes is not null) q = q.Where(a => a.Periodo == mes);
        return q.OrderByDescending(a => a.Periodo).ThenBy(a => a.RegistradoEn)
            .Select(a => new FilaAcompanamiento
            {
                Id = a.Id,
                EstrategiaId = a.EstrategiaId,
                Tipo = a.Tipo,
                Periodo = a.Periodo,
                DocenteIds = a.DocenteIds,
                Seguimientos = a.Seguimientos.Count,
                Avance = a.Seguimientos.OrderByDescending(s => s.Semana).Select(s => s.Avance).FirstOrDefault(),
                UltimaSemana = a.Seguimientos.OrderByDescending(s => s.Semana).Select(s => s.Semana).FirstOrDefault(),
                UltimasActividades = a.Seguimientos.OrderByDescending(s => s.Semana).Select(s => s.Actividades).FirstOrDefault() ?? "",
                UltimaActualizacion = a.Seguimientos.Max(s => (DateTimeOffset?)s.ActualizadoEn),
            })
            .ToListAsync(ct);
    }

    private static ReporteDocumentoDto Documento(TipoReporte tipo, Criterios f, DateTimeOffset ahora, IReadOnlyList<CifraDto> cifras,
        IReadOnlyList<TablaDto> tablas, string nota = NotaAgregados) =>
        new(tipo.Id, tipo.Nombre, tipo.Descripcion, ahora, f.Descripcion, cifras, tablas, nota, tablas[0].Filas.Count == 0);

    private static string Estado(int estudiantes) => estudiantes > 0 ? "Solo consulta" : "Editable";

    // ---------- CU-05 y CU-04: consolidado ----------

    private static async Task<ReporteDocumentoDto> Consolidado(TipoReporte tipo, Criterios f, SsesDbContext db, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var estrategias = await Estrategias(db, f, ct);
        var ids = estrategias.Select(e => e.Id).ToList();
        var acompanamientos = await Acompanamientos(db, ids, f.Mes, ct);
        var porEstrategia = acompanamientos.ToLookup(a => a.EstrategiaId);
        var docentes = await db.Set<EstrategiaDocente>().AsNoTracking().Where(d => ids.Contains(d.EstrategiaId))
            .Select(d => d.DocenteId).Distinct().CountAsync(ct);

        var filas = estrategias.Select(e =>
        {
            var propios = porEstrategia[e.Id].ToList();
            return (IReadOnlyList<string>)
            [
                e.Region, e.Entidad, e.Nombre, e.Periodo, e.Tutor, Estado(e.Estudiantes), F.Entero(e.Estudiantes), F.Entero(e.Docentes),
                F.Entero(propios.Count), F.Entero(propios.Sum(a => a.Seguimientos)), F.Porcentaje(F.Promedio(propios.Select(a => a.Avance))),
            ];
        }).ToList();

        return Documento(tipo, f, ahora,
        [
            new("estrategias", F.Entero(estrategias.Count)),
            new("estudiantes vinculados", F.Entero(estrategias.Sum(e => e.Estudiantes))),
            new("docentes", F.Entero(docentes)),
            new("acompañamientos", F.Entero(acompanamientos.Count)),
            new("seguimientos semanales", F.Entero(acompanamientos.Sum(a => a.Seguimientos))),
            new("avance promedio", F.Porcentaje(F.Promedio(acompanamientos.Select(a => a.Avance))), true),
        ],
        [
            new("Estrategias",
                ["Región", "Entidad", "Estrategia", "Periodo", "Tutor", "Estado", "Estudiantes", "Docentes", "Acompañamientos", "Seguimientos", "Avance"],
                [6, 7, 8, 9, 10], filas),
        ]);
    }

    // ---------- CU-04 · RQ_09: desempeño de tutores ----------

    private static async Task<ReporteDocumentoDto> Desempeno(TipoReporte tipo, Criterios f, SsesDbContext db, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var estrategias = await Estrategias(db, f, ct);
        var acompanamientos = await Acompanamientos(db, estrategias.Select(e => e.Id).ToList(), f.Mes, ct);
        var porEstrategia = acompanamientos.ToLookup(a => a.EstrategiaId);

        var filas = estrategias.GroupBy(e => (e.TutorId, e.Tutor, e.TutorRegion)).OrderBy(g => g.Key.Tutor).Select(g =>
        {
            var propios = g.SelectMany(e => porEstrategia[e.Id]).ToList();
            var ultimo = propios.Max(a => a.UltimaActualizacion);
            return (IReadOnlyList<string>)
            [
                g.Key.Tutor, g.Key.TutorRegion, F.Entero(g.Count()), F.Entero(g.Sum(e => e.Estudiantes)), F.Entero(propios.Count),
                F.Entero(propios.Sum(a => a.Seguimientos)), F.Porcentaje(F.Promedio(propios.Select(a => a.Avance))),
                ultimo is null ? "Sin seguimientos" : F.Fecha(ultimo.Value),
            ];
        }).ToList();

        return Documento(tipo, f, ahora,
        [
            new("tutores", F.Entero(filas.Count)),
            new("estrategias", F.Entero(estrategias.Count)),
            new("estudiantes vinculados", F.Entero(estrategias.Sum(e => e.Estudiantes))),
            new("acompañamientos", F.Entero(acompanamientos.Count)),
            new("seguimientos semanales", F.Entero(acompanamientos.Sum(a => a.Seguimientos))),
            new("avance promedio", F.Porcentaje(F.Promedio(acompanamientos.Select(a => a.Avance))), true),
        ],
        [
            new("Desempeño por tutor",
                ["Tutor", "Región", "Estrategias", "Estudiantes", "Acompañamientos", "Seguimientos", "Avance promedio", "Último seguimiento"],
                [2, 3, 4, 5, 6], filas),
        ]);
    }

    // ---------- CU-04 · RQ_09: actividades realizadas ----------

    private static async Task<ReporteDocumentoDto> Actividades(TipoReporte tipo, Criterios f, SsesDbContext db, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var estrategias = (await Estrategias(db, f, ct)).ToDictionary(e => e.Id);
        var acompanamientos = await Acompanamientos(db, estrategias.Keys, f.Mes, ct);
        var docenteIds = acompanamientos.SelectMany(a => a.DocenteIds).Distinct().ToList();
        var docentes = await db.Docentes.AsNoTracking().Where(d => docenteIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Nombre, ct);

        var filas = acompanamientos.Select(a =>
        {
            var e = estrategias[a.EstrategiaId];
            return (IReadOnlyList<string>)
            [
                F.Mes(a.Periodo), e.Region, e.Entidad, e.Nombre, e.Tutor, a.Tipo.ToString(),
                string.Join(", ", a.DocenteIds.Select(id => docentes.GetValueOrDefault(id, "?"))), F.Entero(a.Seguimientos),
                F.Porcentaje(a.Avance),
                a.Seguimientos == 0 ? "Sin seguimientos" : $"Semana {a.UltimaSemana}: {F.Recortar(a.UltimasActividades, 90)}",
            ];
        }).ToList();

        return Documento(tipo, f, ahora,
        [
            new("acompañamientos", F.Entero(acompanamientos.Count)),
            new("individuales", F.Entero(acompanamientos.Count(a => a.Tipo == TipoAcompanamiento.Individual))),
            new("grupales", F.Entero(acompanamientos.Count(a => a.Tipo == TipoAcompanamiento.Grupal))),
            new("docentes acompañados", F.Entero(docenteIds.Count)),
            new("seguimientos semanales", F.Entero(acompanamientos.Sum(a => a.Seguimientos))),
            new("avance promedio", F.Porcentaje(F.Promedio(acompanamientos.Select(a => a.Avance))), true),
        ],
        [
            new("Acompañamientos",
                ["Mes", "Región", "Entidad", "Estrategia", "Tutor", "Tipo", "Docentes", "Seguimientos", "Avance", "Último seguimiento"],
                [7, 8], filas),
        ]);
    }

    // ---------- CU-04 · RQ_10: reporte de ley de cobertura ----------

    private static async Task<ReporteDocumentoDto> Cobertura(TipoReporte tipo, Criterios f, SsesDbContext db, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var entidadesQ = db.Entidades.AsNoTracking();
        if (f.Region is not null) entidadesQ = entidadesQ.Where(e => e.Region == f.Region);
        if (f.Entidad is not null) entidadesQ = entidadesQ.Where(e => e.Id == f.Entidad.Id);
        var entidades = await entidadesQ.OrderBy(e => e.Region).ThenBy(e => e.Nombre).ToListAsync(ct);
        var ids = entidades.Select(e => e.Id).ToList();

        var estrategias = db.Estrategias.AsNoTracking().Where(e => ids.Contains(e.EntidadId) && e.Periodo == f.Periodo);
        var precargados = await db.Estudiantes.AsNoTracking().Where(s => ids.Contains(s.EntidadId))
            .GroupBy(s => s.EntidadId).Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var docentes = await db.Docentes.AsNoTracking().Where(d => ids.Contains(d.EntidadId))
            .GroupBy(d => d.EntidadId).Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var vinculados = (await estrategias.SelectMany(e => e.Estudiantes.Select(v => new { e.EntidadId, v.EstudianteId })).Distinct().ToListAsync(ct))
            .GroupBy(x => x.EntidadId).ToDictionary(g => g.Key, g => g.Count());
        var enEstrategias = (await estrategias.SelectMany(e => e.Docentes.Select(d => new { e.EntidadId, d.DocenteId })).Distinct().ToListAsync(ct))
            .GroupBy(x => x.EntidadId).ToDictionary(g => g.Key, g => g.Count());
        var porEstrategia = await estrategias.GroupBy(e => e.EntidadId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var acompanados = (await (from a in db.Acompanamientos.AsNoTracking()
                                  join e in estrategias on a.EstrategiaId equals e.Id
                                  select new { e.EntidadId, a.DocenteIds }).ToListAsync(ct))
            .GroupBy(x => x.EntidadId).ToDictionary(g => g.Key, g => g.SelectMany(x => x.DocenteIds).Distinct().Count());

        int De(Dictionary<int, int> d, int id) => d.GetValueOrDefault(id);
        var filas = entidades.Select(e => (IReadOnlyList<string>)
        [
            e.Region, e.Municipio, e.Nombre, F.Entero(De(precargados, e.Id)), F.Entero(De(vinculados, e.Id)),
            F.Porcentaje(F.PorcentajeDe(De(vinculados, e.Id), De(precargados, e.Id))), F.Entero(De(docentes, e.Id)),
            F.Entero(De(enEstrategias, e.Id)), F.Entero(De(acompanados, e.Id)), F.Entero(De(porEstrategia, e.Id)),
        ]).ToList();

        var totalPrecargados = precargados.Values.Sum();
        var totalVinculados = vinculados.Values.Sum();
        return Documento(tipo, f, ahora,
        [
            new("entidades", F.Entero(entidades.Count)),
            new("estudiantes precargados", F.Entero(totalPrecargados)),
            new("estudiantes vinculados", F.Entero(totalVinculados)),
            new("docentes acompañados", F.Entero(acompanados.Values.Sum())),
            new("estrategias", F.Entero(porEstrategia.Values.Sum())),
            new("cobertura", F.Porcentaje(F.PorcentajeDe(totalVinculados, totalPrecargados)), true),
        ],
        [
            new("Cobertura por entidad",
                ["Región", "Municipio", "Entidad", "Estudiantes precargados", "Estudiantes vinculados", "Cobertura", "Docentes",
                    "Docentes en estrategias", "Docentes acompañados", "Estrategias"],
                [3, 4, 5, 6, 7, 8, 9], filas),
        ],
        "Cobertura = estudiantes vinculados a estrategias del periodo / estudiantes precargados de la entidad. " + NotaAgregados);
    }

    // ---------- CU-04 · RQ_10: reporte de ley 1581 de 2012 ----------

    private static async Task<ReporteDocumentoDto> DatosPersonales(TipoReporte tipo, Criterios f, SsesDbContext db, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var estrategias = db.Estrategias.AsNoTracking();
        if (f.Periodo is not null) estrategias = estrategias.Where(e => e.Periodo == f.Periodo);

        var estudiantes = await db.Estudiantes.CountAsync(ct);
        var estudiantesEnUso = await estrategias.SelectMany(e => e.Estudiantes.Select(v => v.EstudianteId)).Distinct().CountAsync(ct);
        var docentes = await db.Docentes.CountAsync(ct);
        var docentesEnUso = await estrategias.SelectMany(e => e.Docentes.Select(d => d.DocenteId)).Distinct().CountAsync(ct);
        var usuarios = await db.Usuarios.CountAsync(ct);
        var usuariosActivos = await db.Auditoria.AsNoTracking().Where(a => a.Operacion == "auth.ingreso" && a.UsuarioId != null)
            .Select(a => a.UsuarioId).Distinct().CountAsync(ct);

        var operaciones = await db.Auditoria.AsNoTracking().GroupBy(a => a.Operacion)
            .Select(g => new { Operacion = g.Key, Total = g.Count(), Usuarios = g.Select(a => a.Usuario).Distinct().Count(), Ultimo = g.Max(a => a.Fecha) })
            .OrderByDescending(x => x.Total).ToListAsync(ct);
        int Total(string operacion) => operaciones.FirstOrDefault(o => o.Operacion == operacion)?.Total ?? 0;

        return Documento(tipo, f, ahora,
        [
            new("titulares registrados", F.Entero(estudiantes + docentes + usuarios)),
            new("menores de edad", F.Entero(estudiantes)),
            new("ingresos registrados", F.Entero(Total("auth.ingreso"))),
            new("intentos fallidos", F.Entero(Total("auth.fallido"))),
            new("bloqueos de cuenta", F.Entero(Total("auth.bloqueo"))),
            new("operaciones registradas", F.Entero(operaciones.Sum(o => o.Total)), true),
        ],
        [
            new("Datos personales tratados", ["Titular", "Datos tratados", "Finalidad", "Titulares", "En uso", "Medidas de protección"], [3, 4],
            [
                ["Estudiantes (menores de edad)", "Tipo y número de documento, nombre", "Vincularlos a las estrategias del programa",
                    F.Entero(estudiantes), F.Entero(estudiantesEnUso), "Documento enmascarado en pantalla; los reportes solo muestran cifras"],
                ["Docentes", "Número de documento, nombre", "Registrar el acompañamiento pedagógico",
                    F.Entero(docentes), F.Entero(docentesEnUso), "Consulta solo por el tutor de la estrategia"],
                ["Usuarios del sistema", "Usuario, nombre, perfil y región", "Autenticación y trazabilidad de operaciones",
                    F.Entero(usuarios), F.Entero(usuariosActivos), "Contraseña cifrada (PBKDF2) y bloqueo tras 5 intentos"],
            ]),
            new("Accesos y operaciones registradas", ["Operación", "Registros", "Usuarios distintos", "Último registro"], [1, 2],
                operaciones.Select(o => (IReadOnlyList<string>)[F.Operacion(o.Operacion), F.Entero(o.Total), F.Entero(o.Usuarios), F.FechaHora(o.Ultimo)])
                    .ToList()),
        ],
        "Inventario de tratamiento según la Ley 1581 de 2012 y el Decreto 1377 de 2013. \"En uso\": titulares vinculados a estrategias"
        + (f.Periodo is null ? "" : $" del periodo {f.Periodo}") + " o usuarios con al menos un ingreso.");
    }
}
