using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sses.Api.Comun;
using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;

namespace Sses.Api.Endpoints;

/// <summary>CU-03: acompañamiento pedagógico mensual, seguimiento semanal e historial.</summary>
public static class AcompanamientosEndpoints
{
    public static void MapAcompanamientos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Acompañamientos").RequireAuthorization(Politicas.Tutor);
        g.MapPost("/acompanamientos", Registrar);
        g.MapGet("/acompanamientos/{id:guid}", Obtener);
        g.MapPut("/acompanamientos/{id:guid}/seguimientos/{semana:int}", RegistrarSeguimiento);
        g.MapGet("/estrategias/{id:int}/acompanamientos", Historial);
    }

    private static IQueryable<Acompanamiento> DelTutor(SsesDbContext db, ClaimsPrincipal user)
    {
        var tutorId = user.UsuarioId();
        return db.Acompanamientos.Include(a => a.Seguimientos)
            .Where(a => db.Estrategias.Any(e => e.Id == a.EstrategiaId && e.TutorId == tutorId));
    }

    private static IResult NoEncontrado() =>
        Problema.NoEncontrado("acompanamiento_no_encontrado", "No encontramos ese acompañamiento entre los tuyos.");

    private static async Task<IResult> Registrar(AcompanamientoRequest req, ClaimsPrincipal user, SsesDbContext db, Auditor auditor,
        TimeProvider reloj, CancellationToken ct)
    {
        var estrategia = await EstrategiasEndpoints.EstrategiaPropia(db, req.EstrategiaId, user, ct);
        if (estrategia is null) return EstrategiasEndpoints.EstrategiaNoEncontrada();

        // Reenvío desde la cola sin conexión: mismo Id, misma respuesta (idempotente).
        if (req.Id != Guid.Empty)
        {
            var mismo = await DelTutor(db, user).SingleOrDefaultAsync(a => a.Id == req.Id, ct);
            if (mismo is not null) return Results.Ok(await mismo.ADtoAsync(db, ct, existente: true));
        }

        var acompanamiento = Acompanamiento.Registrar(req.Id, estrategia, req.Tipo, LeerPeriodo(req.Periodo),
            req.Objetivo ?? "", req.Compromisos, req.DocenteIds ?? [], user.UsuarioId(), reloj.GetUtcNow());

        // Flujo alterno 4A: ese docente o grupo ya tiene acompañamiento este mes.
        var existente = await BuscarDelMismoGrupo(db, acompanamiento, ct);
        if (existente is not null) return Results.Ok(await existente.ADtoAsync(db, ct, existente: true));

        db.Acompanamientos.Add(acompanamiento);
        auditor.Registrar("acompanamiento.registrar", "acompanamiento", acompanamiento.Id,
            $"{acompanamiento.Tipo} {Mapeos.FormatoPeriodo(acompanamiento.Periodo)}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos dispositivos sincronizaron el mismo acompañamiento a la vez: gana el primero.
            db.ChangeTracker.Clear();
            var ganador = await BuscarDelMismoGrupo(db, acompanamiento, ct)
                          ?? await DelTutor(db, user).SingleAsync(a => a.Id == acompanamiento.Id, ct);
            return Results.Ok(await ganador.ADtoAsync(db, ct, existente: true));
        }

        return Results.Created($"/api/acompanamientos/{acompanamiento.Id}", await acompanamiento.ADtoAsync(db, ct));
    }

    private static Task<Acompanamiento?> BuscarDelMismoGrupo(SsesDbContext db, Acompanamiento a, CancellationToken ct) =>
        db.Acompanamientos.Include(x => x.Seguimientos).SingleOrDefaultAsync(x =>
            x.EstrategiaId == a.EstrategiaId && x.Periodo == a.Periodo && x.Tipo == a.Tipo && x.ClaveGrupo == a.ClaveGrupo, ct);

    private static async Task<IResult> Obtener(Guid id, ClaimsPrincipal user, SsesDbContext db, CancellationToken ct)
    {
        var acompanamiento = await DelTutor(db, user).AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        return acompanamiento is null ? NoEncontrado() : Results.Ok(await acompanamiento.ADtoAsync(db, ct));
    }

    private static async Task<IResult> RegistrarSeguimiento(Guid id, int semana, SeguimientoRequest req, ClaimsPrincipal user,
        SsesDbContext db, Auditor auditor, TimeProvider reloj, CancellationToken ct)
    {
        var acompanamiento = await DelTutor(db, user).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (acompanamiento is null) return NoEncontrado();

        var antes = acompanamiento.Seguimientos.Count;
        acompanamiento.RegistrarSeguimiento(req.Id, semana, req.Actividades ?? "", req.Avance, req.Observaciones, reloj.GetUtcNow());
        var operacion = acompanamiento.Seguimientos.Count > antes ? "seguimiento.registrar" : "seguimiento.actualizar";
        auditor.Registrar(operacion, "acompanamiento", acompanamiento.Id, $"semana {semana}, avance {req.Avance} %");
        await db.SaveChangesAsync(ct);

        return Results.Ok(await acompanamiento.ADtoAsync(db, ct));
    }

    private static async Task<IResult> Historial(int id, int? docenteId, string? grupo, ClaimsPrincipal user, SsesDbContext db,
        CancellationToken ct)
    {
        var estrategia = await EstrategiasEndpoints.EstrategiaPropia(db, id, user, ct);
        if (estrategia is null) return EstrategiasEndpoints.EstrategiaNoEncontrada();

        var consulta = db.Acompanamientos.AsNoTracking().Include(a => a.Seguimientos).Where(a => a.EstrategiaId == id);
        if (docenteId is not null) consulta = consulta.Where(a => a.DocenteIds.Contains(docenteId.Value));
        if (!string.IsNullOrWhiteSpace(grupo)) consulta = consulta.Where(a => a.ClaveGrupo == grupo);

        var lista = await consulta.OrderByDescending(a => a.Periodo).ThenByDescending(a => a.RegistradoEn).ToListAsync(ct);
        return Results.Ok(await lista.ADtoAsync(db, ct));
    }

    private static DateOnly LeerPeriodo(string? periodo) =>
        DateOnly.TryParseExact($"{periodo?.Trim()}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : throw new ReglaNegocioException(Errores.DatosIncompletos, "Indica el mes del acompañamiento (AAAA-MM).", ["periodo"]);
}
