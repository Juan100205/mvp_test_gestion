using Microsoft.EntityFrameworkCore;
using Sses.Domain;
using Sses.Infrastructure;

namespace Sses.Api.Comun;

public record LoginRequest(string? Username, string? Password);
public record UsuarioDto(int Id, string Username, string Nombre, Rol Rol, string Region);
public record SesionDto(string Token, DateTimeOffset Expira, UsuarioDto Usuario);

public record EntidadDto(int Id, string Nombre, string Region, string Municipio);
public record DocenteDto(int Id, string Documento, string Nombre);
public record EstudianteDto(int Id, string TipoDocumento, string NumeroDocumento, string Nombre, DateTimeOffset? VinculadoEn);

public record EstrategiaRequest(int EntidadId, string? Nombre, string? Descripcion, string? Periodo, int[]? DocenteIds);
public record VincularEstudianteRequest(int EstudianteId);
public record EstrategiaResumenDto(int Id, string Nombre, string Periodo, string Entidad, string Region, EstadoEstrategia Estado,
    int TotalEstudiantes, int TotalDocentes, int TotalAcompanamientos, DateTimeOffset ActualizadaEn);
public record EstrategiaDto(int Id, string Nombre, string Descripcion, string Periodo, EntidadDto Entidad, EstadoEstrategia Estado,
    bool EsEditable, IReadOnlyList<DocenteDto> Docentes, IReadOnlyList<EstudianteDto> Estudiantes);

public record AcompanamientoRequest(Guid Id, int EstrategiaId, TipoAcompanamiento Tipo, string? Periodo, string? Objetivo,
    string? Compromisos, int[]? DocenteIds);
public record SeguimientoRequest(Guid Id, string? Actividades, int Avance, string? Observaciones);
public record SeguimientoDto(Guid Id, int Semana, string Actividades, int Avance, string Observaciones, DateTimeOffset ActualizadoEn);
public record AcompanamientoDto(Guid Id, int EstrategiaId, TipoAcompanamiento Tipo, string Periodo, string Objetivo, string Compromisos,
    IReadOnlyList<DocenteDto> Docentes, int AvanceActual, IReadOnlyList<SeguimientoDto> Seguimientos, DateTimeOffset RegistradoEn, bool Existente);

public record AuditoriaDto(long Id, string Usuario, string Operacion, string Recurso, string? RecursoId, string? Detalle, DateTimeOffset Fecha);

public static class Mapeos
{
    public static UsuarioDto ADto(this Usuario u) => new(u.Id, u.Username, u.Nombre, u.Rol, u.Region);

    public static string FormatoPeriodo(DateOnly periodo) => periodo.ToString("yyyy-MM");

    public static async Task<EstrategiaDto> ADtoAsync(this Estrategia e, SsesDbContext db, CancellationToken ct)
    {
        var entidad = await db.Entidades.AsNoTracking().SingleAsync(x => x.Id == e.EntidadId, ct);
        var docenteIds = e.Docentes.Select(d => d.DocenteId).ToList();
        var docentes = await db.Docentes.AsNoTracking()
            .Where(d => docenteIds.Contains(d.Id)).OrderBy(d => d.Nombre)
            .Select(d => new DocenteDto(d.Id, d.Documento, d.Nombre)).ToListAsync(ct);

        var vinculos = e.Estudiantes.ToDictionary(v => v.EstudianteId, v => v.VinculadoEn);
        var estudiantes = (await db.Estudiantes.AsNoTracking()
                .Where(s => vinculos.Keys.Contains(s.Id)).ToListAsync(ct))
            .Select(s => new EstudianteDto(s.Id, s.TipoDocumento, s.NumeroDocumento, s.Nombre, vinculos[s.Id]))
            .OrderByDescending(s => s.VinculadoEn).ToList();

        return new EstrategiaDto(e.Id, e.Nombre, e.Descripcion, e.Periodo,
            new EntidadDto(entidad.Id, entidad.Nombre, entidad.Region, entidad.Municipio),
            e.Estado, e.EsEditable, docentes, estudiantes);
    }

    public static async Task<List<AcompanamientoDto>> ADtoAsync(this IReadOnlyCollection<Acompanamiento> lista, SsesDbContext db,
        CancellationToken ct, bool existente = false)
    {
        var ids = lista.SelectMany(a => a.DocenteIds).Distinct().ToList();
        var docentes = await db.Docentes.AsNoTracking().Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new DocenteDto(d.Id, d.Documento, d.Nombre), ct);

        return lista.Select(a => new AcompanamientoDto(a.Id, a.EstrategiaId, a.Tipo, FormatoPeriodo(a.Periodo), a.Objetivo, a.Compromisos,
            a.DocenteIds.Select(id => docentes[id]).ToList(), a.AvanceActual,
            a.Seguimientos.OrderBy(s => s.Semana)
                .Select(s => new SeguimientoDto(s.Id, s.Semana, s.Actividades, s.Avance, s.Observaciones, s.ActualizadoEn)).ToList(),
            a.RegistradoEn, existente)).ToList();
    }

    public static async Task<AcompanamientoDto> ADtoAsync(this Acompanamiento a, SsesDbContext db, CancellationToken ct, bool existente = false) =>
        (await new[] { a }.ADtoAsync(db, ct, existente))[0];
}
