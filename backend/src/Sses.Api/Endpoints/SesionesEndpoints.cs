using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sses.Api.Comun;
using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;
using F = Sses.Api.Comun.Formato;

namespace Sses.Api.Endpoints;

/// <summary>CU-07: sesiones de pilotaje, asistencia, uso del sistema, incidentes y encuesta de percepción (RQ_15, RQ_16).</summary>
public static class SesionesEndpoints
{
    private static readonly string[] IncidentesAutomaticos = ["auth.fallido", "auth.bloqueo"];

    public static void MapSesiones(this IEndpointRouteBuilder app)
    {
        var lider = app.MapGroup("/api/sesiones").WithTags("Sesiones de pilotaje").RequireAuthorization(Politicas.LiderPilotaje);
        lider.MapGet("/", Listar);
        lider.MapGet("/tutores", TutoresElegibles);
        lider.MapPost("/", Programar);
        lider.MapGet("/{id:int}", Obtener);
        lider.MapGet("/{id:int}/consolidado", Consolidado);
        lider.MapPost("/{id:int}/incidentes", RegistrarIncidente);
        lider.MapPost("/{id:int}/descargas", RegistrarExportacion);

        var tutor = app.MapGroup("/api").WithTags("Sesiones de pilotaje").RequireAuthorization(Politicas.Tutor);
        tutor.MapGet("/mis-sesiones", MisSesiones);
        tutor.MapPost("/sesiones/{id:int}/asistencia", RegistrarAsistencia);
        tutor.MapPost("/sesiones/{id:int}/encuesta", ResponderEncuesta);

        app.MapGet("/api/sesiones/preguntas", () => SesionPilotaje.Preguntas.Select(p => new PreguntaDto(p.Clave, p.Texto)))
            .WithTags("Sesiones de pilotaje").RequireAuthorization();
    }

    private static IQueryable<SesionPilotaje> ConDetalle(SsesDbContext db) =>
        db.Sesiones.Include(s => s.Asistencias).Include(s => s.Incidentes).Include(s => s.Encuestas);

    private static IResult SesionNoEncontrada() => Problema.NoEncontrado("sesion_no_encontrada", "No encontramos esa sesión de pilotaje.");

    // ---------- Líder de pilotaje ----------

    private static async Task<IResult> Listar(SsesDbContext db, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var sesiones = await ConDetalle(db).AsNoTracking().OrderByDescending(s => s.Inicio).ToListAsync(ct);
        return Results.Ok(sesiones.Select(s => new SesionResumenDto(s.Id, s.Nombre, s.Region, s.Inicio, s.Fin, s.Estado(ahora),
            s.Convocados.Count, s.Asistencias.Count, s.Encuestas.Count)));
    }

    private static async Task<IResult> TutoresElegibles(string? region, SsesDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Usuarios.AsNoTracking()
            .Where(u => u.Rol == Rol.Tutor && (region == null || u.Region == region))
            .OrderBy(u => u.Nombre).Select(u => new TutorReporteDto(u.Id, u.Nombre, u.Region)).ToListAsync(ct));

    private static async Task<IResult> Programar(ProgramarSesionRequest req, ClaimsPrincipal user, SsesDbContext db, Auditor auditor,
        TimeProvider reloj, CancellationToken ct)
    {
        var ids = (req.TutorIds ?? []).Distinct().ToList();
        var tutores = await db.Usuarios.Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        if (tutores.Count != ids.Count)
            throw new ReglaNegocioException(Errores.TutorDeOtraRegion, "Alguno de los tutores elegidos no existe.", ["tutores"]);

        var sesion = SesionPilotaje.Programar(req.Nombre ?? "", req.Region ?? "", req.Inicio, req.DuracionMinutos, tutores,
            user.UsuarioId(), reloj.GetUtcNow());
        db.Sesiones.Add(sesion);
        await db.SaveChangesAsync(ct);
        // La convocatoria queda visible para cada tutor en "Mis sesiones" (notificación dentro del sistema).
        auditor.Registrar("sesion.programar", "sesion", sesion.Id, $"{sesion.Nombre} · {sesion.Region} · {F.FechaHora(sesion.Inicio)}");
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/sesiones/{sesion.Id}", await Detalle(sesion, db, reloj.GetUtcNow(), ct));
    }

    private static async Task<IResult> Obtener(int id, SsesDbContext db, TimeProvider reloj, CancellationToken ct)
    {
        var sesion = await ConDetalle(db).AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
        return sesion is null ? SesionNoEncontrada() : Results.Ok(await Detalle(sesion, db, reloj.GetUtcNow(), ct));
    }

    private static async Task<SesionDetalleDto> Detalle(SesionPilotaje s, SsesDbContext db, DateTimeOffset ahora, CancellationToken ct)
    {
        var nombres = await db.Usuarios.AsNoTracking().Where(u => s.Convocados.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        var convocados = s.Convocados.Select(id => new ConvocadoDto(id, nombres.GetValueOrDefault(id, "?"),
                s.Asistencias.FirstOrDefault(a => a.TutorId == id)?.IngresoEn, s.RespondioEncuesta(id)))
            .OrderBy(c => c.Nombre).ToList();
        return new SesionDetalleDto(s.Id, s.Nombre, s.Region, s.Inicio, s.Fin, s.Estado(ahora), convocados,
            s.Incidentes.OrderBy(i => i.RegistradoEn).Select(i => new IncidenteDto(i.Id, i.Descripcion, i.RegistradoEn)).ToList());
    }

    private static async Task<IResult> RegistrarIncidente(int id, IncidenteRequest req, SsesDbContext db, Auditor auditor, TimeProvider reloj,
        CancellationToken ct)
    {
        var sesion = await db.Sesiones.Include(s => s.Incidentes).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (sesion is null) return SesionNoEncontrada();

        var incidente = sesion.RegistrarIncidente(req.Descripcion ?? "", reloj.GetUtcNow());
        auditor.Registrar("sesion.incidente", "sesion", sesion.Id, F.Recortar(incidente.Descripcion, 500));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/sesiones/{id}", new IncidenteDto(incidente.Id, incidente.Descripcion, incidente.RegistradoEn));
    }

    private static async Task<IResult> RegistrarExportacion(int id, ExportarSesionRequest req, SsesDbContext db, Auditor auditor,
        CancellationToken ct)
    {
        var formato = req.Formato?.Trim().ToLowerInvariant();
        if (formato is not ("csv" or "pdf"))
            return Problema.Crear(StatusCodes.Status400BadRequest, "formato_no_valido", "El registro de la sesión se exporta en CSV o PDF.");
        if (!await db.Sesiones.AnyAsync(s => s.Id == id, ct)) return SesionNoEncontrada();

        auditor.Registrar("sesion.exportar", "sesion", id, formato);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>CU-07 paso 6: asistencia, uso del sistema, incidentes y encuesta, como documento exportable.</summary>
    private static async Task<IResult> Consolidado(int id, SsesDbContext db, TimeProvider reloj, CancellationToken ct)
    {
        var s = await ConDetalle(db).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return SesionNoEncontrada();

        var ahora = reloj.GetUtcNow();
        var desde = s.Inicio - SesionPilotaje.IngresoAnticipado;
        var tutores = await db.Usuarios.AsNoTracking().Where(u => s.Convocados.Contains(u.Id)).OrderBy(u => u.Nombre).ToListAsync(ct);
        var usernames = tutores.Select(t => t.Username).ToList();

        var metricas = await db.Metricas.AsNoTracking()
            .Where(m => s.Convocados.Contains(m.UsuarioId) && m.Fecha >= desde && m.Fecha <= s.Fin)
            .GroupBy(m => m.UsuarioId)
            .Select(g => new { g.Key, Peticiones = g.Count(), Promedio = g.Average(m => m.Milisegundos), Maximo = g.Max(m => m.Milisegundos), Errores = g.Count(m => m.Estado >= 500) })
            .ToDictionaryAsync(x => x.Key, ct);
        var operaciones = await db.Auditoria.AsNoTracking()
            .Where(a => a.UsuarioId != null && s.Convocados.Contains(a.UsuarioId.Value) && a.Fecha >= desde && a.Fecha <= s.Fin)
            .GroupBy(a => a.UsuarioId!.Value).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var automaticos = await db.Auditoria.AsNoTracking()
            .Where(a => IncidentesAutomaticos.Contains(a.Operacion) && usernames.Contains(a.Usuario) && a.Fecha >= desde && a.Fecha <= s.Fin)
            .OrderBy(a => a.Fecha).ToListAsync(ct);
        var erroresServidor = metricas.Values.Sum(m => m.Errores);

        var asistencia = tutores.Select(t =>
        {
            var ingreso = s.Asistencias.FirstOrDefault(a => a.TutorId == t.Id)?.IngresoEn;
            var m = metricas.GetValueOrDefault(t.Id);
            return (IReadOnlyList<string>)
            [
                t.Nombre, ingreso is null ? "No" : "Sí", ingreso is null ? "—" : F.Hora(ingreso.Value), F.Entero(operaciones.GetValueOrDefault(t.Id)),
                F.Entero(m?.Peticiones ?? 0), m is null ? "—" : F.Entero((int)Math.Round(m.Promedio)), m is null ? "—" : F.Entero(m.Maximo),
                s.RespondioEncuesta(t.Id) ? "Sí" : "No",
            ];
        }).ToList();

        var incidentes = automaticos.Select(a => (Fecha: a.Fecha, Fila: (IReadOnlyList<string>)[F.Hora(a.Fecha), "Automático", $"{F.Operacion(a.Operacion)} ({a.Usuario})"]))
            .Concat(s.Incidentes.Select(i => (Fecha: i.RegistradoEn, Fila: (IReadOnlyList<string>)[F.Hora(i.RegistradoEn), "Manual", i.Descripcion])))
            .OrderBy(x => x.Fecha).Select(x => x.Fila).ToList();
        if (erroresServidor > 0)
            incidentes.Add(["—", "Automático", $"{erroresServidor} respuestas con error del servidor durante la sesión"]);

        var encuesta = SesionPilotaje.Preguntas.Select((p, i) =>
        {
            var respuestas = s.Encuestas.Select(e => e.Respuestas[i]).ToList();
            return (IReadOnlyList<string>)
            [
                p.Texto, F.Entero(respuestas.Count), respuestas.Count == 0 ? "—" : F.Decimal1(respuestas.Average()),
                .. Enumerable.Range(1, 5).Select(v => F.Entero(respuestas.Count(r => r == v))),
            ];
        }).ToList();
        var comentarios = s.Encuestas.Where(e => e.Comentario.Length > 0).OrderBy(e => e.RespondidaEn)
            .Select(e => (IReadOnlyList<string>)[e.Comentario]).ToList();
        var satisfaccion = s.Encuestas.Count == 0 ? "—" : F.Decimal1(s.Encuestas.SelectMany(e => e.Respuestas).Average());

        var documento = new ReporteDocumentoDto("sesion", s.Nombre,
            $"Sesión de pilotaje de la región {s.Region}: {F.FechaHora(s.Inicio)} a {F.Hora(s.Fin)}.", ahora,
            $"Región: {s.Region} · Estado: {s.Estado(ahora) switch { EstadoSesion.Programada => "programada", EstadoSesion.EnCurso => "en curso", _ => "terminada" }}",
            [
                new("convocados", F.Entero(s.Convocados.Count)),
                new("asistieron", F.Entero(s.Asistencias.Count)),
                new("asistencia", F.Porcentaje(F.PorcentajeDe(s.Asistencias.Count, s.Convocados.Count)), true),
                new("encuestas respondidas", F.Entero(s.Encuestas.Count)),
                new("satisfacción promedio (1 a 5)", satisfaccion),
                new("incidentes", F.Entero(incidentes.Count)),
            ],
            [
                new("Asistencia y uso del sistema",
                    ["Tutor", "Asistió", "Hora de ingreso", "Operaciones", "Peticiones", "Tiempo promedio (ms)", "Tiempo máximo (ms)", "Encuesta"],
                    [3, 4, 5, 6], asistencia),
                new("Incidentes", ["Hora", "Origen", "Descripción"], [], incidentes),
                new("Encuesta de percepción", ["Pregunta", "Respuestas", "Promedio", "1", "2", "3", "4", "5"], [1, 2, 3, 4, 5, 6, 7], encuesta),
                new("Comentarios", ["Comentario"], [], comentarios),
            ],
            "Uso y tiempos de respuesta medidos por el servidor desde 30 minutos antes del inicio hasta el fin de la sesión. Los comentarios son anónimos.",
            false);
        return Results.Ok(documento);
    }

    // ---------- Tutor ----------

    private static MiSesionDto Mia(SesionPilotaje s, int tutorId, DateTimeOffset ahora) =>
        new(s.Id, s.Nombre, s.Region, s.Inicio, s.Fin, s.Estado(ahora), s.PermiteIngreso(ahora) && !s.Asistio(tutorId),
            s.Asistencias.FirstOrDefault(a => a.TutorId == tutorId)?.IngresoEn, s.RespondioEncuesta(tutorId));

    private static async Task<IResult> MisSesiones(ClaimsPrincipal user, SsesDbContext db, TimeProvider reloj, CancellationToken ct)
    {
        var tutorId = user.UsuarioId();
        var ahora = reloj.GetUtcNow();
        var sesiones = await ConDetalle(db).AsNoTracking().Where(s => s.Convocados.Contains(tutorId)).OrderBy(s => s.Inicio).ToListAsync(ct);
        return Results.Ok(sesiones.Select(s => Mia(s, tutorId, ahora)));
    }

    private static async Task<IResult> RegistrarAsistencia(int id, ClaimsPrincipal user, SsesDbContext db, Auditor auditor, TimeProvider reloj,
        CancellationToken ct)
    {
        var tutorId = user.UsuarioId();
        var ahora = reloj.GetUtcNow();
        var sesion = await ConDetalle(db).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (sesion is null) return SesionNoEncontrada();

        // Flujo alterno 3A: se informa la sesión asignada al tutor.
        if (!sesion.EstaConvocado(tutorId))
        {
            var asignada = await db.Sesiones.AsNoTracking().Where(s => s.Convocados.Contains(tutorId) && s.Fin >= ahora)
                .OrderBy(s => s.Inicio).FirstOrDefaultAsync(ct);
            return Problema.Crear(StatusCodes.Status403Forbidden, Errores.NoConvocado,
                asignada is null
                    ? "No estás convocado a esta sesión y no tienes otra sesión asignada."
                    : $"No estás convocado a esta sesión. Tu sesión asignada es «{asignada.Nombre}», el {F.FechaHora(asignada.Inicio)}.",
                new Dictionary<string, object?>
                {
                    ["sesionAsignada"] = asignada is null ? null : new { asignada.Id, asignada.Nombre, asignada.Inicio },
                });
        }

        var yaAsistio = sesion.Asistio(tutorId);
        sesion.RegistrarAsistencia(tutorId, ahora);
        if (!yaAsistio)
        {
            auditor.Registrar("sesion.asistencia", "sesion", sesion.Id, sesion.Nombre);
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok(Mia(sesion, tutorId, ahora));
    }

    private static async Task<IResult> ResponderEncuesta(int id, EncuestaRequest req, ClaimsPrincipal user, SsesDbContext db, Auditor auditor,
        TimeProvider reloj, CancellationToken ct)
    {
        var tutorId = user.UsuarioId();
        var sesion = await ConDetalle(db).SingleOrDefaultAsync(s => s.Id == id && s.Convocados.Contains(tutorId), ct);
        if (sesion is null) return SesionNoEncontrada();

        sesion.ResponderEncuesta(tutorId, req.Respuestas ?? [], req.Comentario, reloj.GetUtcNow());
        auditor.Registrar("sesion.encuesta", "sesion", sesion.Id, sesion.Nombre);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/mis-sesiones", Mia(sesion, tutorId, reloj.GetUtcNow()));
    }
}
