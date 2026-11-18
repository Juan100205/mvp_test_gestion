using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sses.Api.Comun;
using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;

namespace Sses.Api.Endpoints;

/// <summary>CU-01 (estrategias) y CU-02 (asociación de estudiantes precargados).</summary>
public static class EstrategiasEndpoints
{
    public static void MapEstrategias(this IEndpointRouteBuilder app)
    {
        var catalogos = app.MapGroup("/api").WithTags("Catálogos precargados").RequireAuthorization(Politicas.Tutor);
        catalogos.MapGet("/entidades", Entidades);
        catalogos.MapGet("/entidades/{id:int}/docentes", DocentesDeEntidad);
        catalogos.MapGet("/estudiantes", BuscarEstudiante);

        var g = app.MapGroup("/api/estrategias").WithTags("Estrategias").RequireAuthorization(Politicas.Tutor);
        g.MapGet("/", Listar);
        g.MapGet("/{id:int}", Obtener);
        g.MapPost("/", Crear);
        g.MapPut("/{id:int}", Editar);
        g.MapPost("/{id:int}/estudiantes", VincularEstudiante);
    }

    internal static Task<Estrategia?> EstrategiaPropia(SsesDbContext db, int id, ClaimsPrincipal user, CancellationToken ct) =>
        db.Estrategias.Include(e => e.Docentes).Include(e => e.Estudiantes)
            .SingleOrDefaultAsync(e => e.Id == id && e.TutorId == user.UsuarioId(), ct);

    internal static IResult EstrategiaNoEncontrada() =>
        Problema.NoEncontrado("estrategia_no_encontrada", "No encontramos esa estrategia entre las tuyas.");

    private static async Task<IResult> Entidades(SsesDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Entidades.AsNoTracking().OrderBy(e => e.Region).ThenBy(e => e.Nombre)
            .Select(e => new EntidadDto(e.Id, e.Nombre, e.Region, e.Municipio)).ToListAsync(ct));

    private static async Task<IResult> DocentesDeEntidad(int id, SsesDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Docentes.AsNoTracking().Where(d => d.EntidadId == id).OrderBy(d => d.Nombre)
            .Select(d => new DocenteDto(d.Id, d.Documento, d.Nombre)).ToListAsync(ct));

    private static async Task<IResult> BuscarEstudiante(string? documento, SsesDbContext db, CancellationToken ct)
    {
        var numero = documento?.Trim() ?? "";
        var estudiante = numero.Length == 0
            ? null
            : await db.Estudiantes.AsNoTracking().SingleOrDefaultAsync(s => s.NumeroDocumento == numero, ct);

        return estudiante is null
            ? Problema.NoEncontrado("estudiante_no_encontrado",
                "No hay un estudiante precargado con ese documento. Verifica el número; el piloto no crea estudiantes nuevos.")
            : Results.Ok(new EstudianteDto(estudiante.Id, estudiante.TipoDocumento, estudiante.NumeroDocumento, estudiante.Nombre, null));
    }

    private static async Task<IResult> Listar(ClaimsPrincipal user, SsesDbContext db, CancellationToken ct)
    {
        var tutorId = user.UsuarioId();
        var filas = await (
                from e in db.Estrategias.AsNoTracking()
                join en in db.Entidades on e.EntidadId equals en.Id
                where e.TutorId == tutorId
                orderby e.ActualizadaEn descending
                select new
                {
                    e.Id, e.Nombre, e.Periodo, Entidad = en.Nombre, en.Region, e.ActualizadaEn,
                    Estudiantes = e.Estudiantes.Count,
                    Docentes = e.Docentes.Count,
                    Acompanamientos = db.Acompanamientos.Count(a => a.EstrategiaId == e.Id),
                })
            .ToListAsync(ct);

        return Results.Ok(filas.Select(f => new EstrategiaResumenDto(f.Id, f.Nombre, f.Periodo, f.Entidad, f.Region,
            f.Estudiantes == 0 ? EstadoEstrategia.Editable : EstadoEstrategia.SoloConsulta,
            f.Estudiantes, f.Docentes, f.Acompanamientos, f.ActualizadaEn)));
    }

    private static async Task<IResult> Obtener(int id, ClaimsPrincipal user, SsesDbContext db, CancellationToken ct)
    {
        var estrategia = await EstrategiaPropia(db, id, user, ct);
        return estrategia is null ? EstrategiaNoEncontrada() : Results.Ok(await estrategia.ADtoAsync(db, ct));
    }

    private static async Task<IResult> Crear(EstrategiaRequest req, ClaimsPrincipal user, SsesDbContext db, Auditor auditor,
        TimeProvider reloj, CancellationToken ct)
    {
        var entidad = await db.Entidades.SingleOrDefaultAsync(e => e.Id == req.EntidadId, ct);
        if (entidad is null)
            throw new ReglaNegocioException(Errores.EntidadNoValida, "Selecciona una de las entidades definidas.", ["entidadId"]);

        var docentes = await DocentesSolicitados(db, req.DocenteIds, ct);
        var estrategia = Estrategia.Crear(entidad, user.UsuarioId(), req.Nombre ?? "", req.Descripcion, req.Periodo ?? "", docentes, reloj.GetUtcNow());

        // El Id lo asigna la BD; la transacción mantiene juntos la estrategia y su registro de auditoría.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        db.Estrategias.Add(estrategia);
        await db.SaveChangesAsync(ct);
        auditor.Registrar("estrategia.crear", "estrategia", estrategia.Id, estrategia.Nombre);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return Results.Created($"/api/estrategias/{estrategia.Id}", await estrategia.ADtoAsync(db, ct));
    }

    private static async Task<IResult> Editar(int id, EstrategiaRequest req, ClaimsPrincipal user, SsesDbContext db, Auditor auditor,
        TimeProvider reloj, CancellationToken ct)
    {
        var estrategia = await EstrategiaPropia(db, id, user, ct);
        if (estrategia is null) return EstrategiaNoEncontrada();

        var docentes = await DocentesSolicitados(db, req.DocenteIds, ct);
        estrategia.Editar(req.Nombre ?? "", req.Descripcion, req.Periodo ?? "", docentes, reloj.GetUtcNow());
        auditor.Registrar("estrategia.editar", "estrategia", estrategia.Id, estrategia.Nombre);
        await db.SaveChangesAsync(ct);

        return Results.Ok(await estrategia.ADtoAsync(db, ct));
    }

    private static async Task<IResult> VincularEstudiante(int id, VincularEstudianteRequest req, ClaimsPrincipal user, SsesDbContext db,
        Auditor auditor, TimeProvider reloj, CancellationToken ct)
    {
        var estrategia = await EstrategiaPropia(db, id, user, ct);
        if (estrategia is null) return EstrategiaNoEncontrada();

        var estudiante = await db.Estudiantes.SingleOrDefaultAsync(s => s.Id == req.EstudianteId, ct);
        if (estudiante is null)
            return Problema.NoEncontrado("estudiante_no_encontrado", "Ese estudiante no está en la base precargada.");

        estrategia.VincularEstudiante(estudiante, reloj.GetUtcNow());
        auditor.Registrar("estrategia.vincular_estudiante", "estrategia", estrategia.Id, $"estudiante {estudiante.NumeroDocumento}");
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/estrategias/{estrategia.Id}", await estrategia.ADtoAsync(db, ct));
    }

    private static async Task<List<Docente>> DocentesSolicitados(SsesDbContext db, int[]? ids, CancellationToken ct)
    {
        var solicitados = (ids ?? []).Distinct().ToList();
        var docentes = await db.Docentes.Where(d => solicitados.Contains(d.Id)).ToListAsync(ct);
        if (docentes.Count != solicitados.Count)
            throw new ReglaNegocioException(Errores.DocenteNoAsociado, "Uno de los docentes seleccionados no está registrado.", ["docentes"]);
        return docentes;
    }
}
