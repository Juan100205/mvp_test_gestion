using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sses.Api.Tests.Infra;

// Contrato HTTP que consume el front. Si una prueba falla aquí, el front también se rompe.

public static class Json
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> Leer<T>(this HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<T>(Opciones))!;
}

public record UsuarioDto(int Id, string Nombre, string Rol, string Region);
public record SesionDto(string Token, DateTimeOffset Expira, UsuarioDto Usuario);
public record ErrorDto(string Codigo, string Detail, int Status, string[]? Campos, int? IntentosRestantes, DateTimeOffset? BloqueadoHasta);

public record EntidadDto(int Id, string Nombre, string Region, string Municipio);
public record DocenteDto(int Id, string Documento, string Nombre);
public record EstudianteDto(int Id, string TipoDocumento, string NumeroDocumento, string Nombre, DateTimeOffset? VinculadoEn);

public record EstrategiaResumenDto(int Id, string Nombre, string Periodo, string Entidad, string Estado, int TotalEstudiantes, int TotalDocentes);
public record EstrategiaDto(int Id, string Nombre, string Descripcion, string Periodo, EntidadDto Entidad, string Estado, bool EsEditable,
    DocenteDto[] Docentes, EstudianteDto[] Estudiantes);

public record SeguimientoDto(Guid Id, int Semana, string Actividades, int Avance, string Observaciones, DateTimeOffset ActualizadoEn);
public record AcompanamientoDto(Guid Id, int EstrategiaId, string Tipo, string Periodo, string Objetivo, string Compromisos,
    DocenteDto[] Docentes, int AvanceActual, SeguimientoDto[] Seguimientos, bool Existente);

public record AuditoriaDto(long Id, string Usuario, string Operacion, string Recurso, string? RecursoId, string? Detalle, DateTimeOffset Fecha);

public record NuevaEstrategia(int EntidadId, string Nombre, string Descripcion, string Periodo, int[] DocenteIds);
