using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sses.Domain;

namespace Sses.Api.Comun;

/// <summary>Respuestas de error con ProblemDetails (RFC 9457) y un código estable que el front traduce.</summary>
public static class Problema
{
    public static IResult Crear(int status, string codigo, string detalle, IDictionary<string, object?>? extras = null)
    {
        var extensiones = new Dictionary<string, object?> { ["codigo"] = codigo };
        if (extras is not null)
            foreach (var (clave, valor) in extras) extensiones[clave] = valor;
        return Results.Problem(statusCode: status, detail: detalle, extensions: extensiones);
    }

    public static IResult NoEncontrado(string codigo, string detalle) => Crear(StatusCodes.Status404NotFound, codigo, detalle);
}

public sealed class ManejadorErrores(IProblemDetailsService problemas, ILogger<ManejadorErrores> log) : IExceptionHandler
{
    private static readonly HashSet<string> Conflictos =
    [
        Errores.EstrategiaBloqueada, Errores.EstudianteYaVinculado, Errores.SesionFueraDeHorario, Errores.EncuestaYaRespondida,
        Errores.SinAsistencia,
    ];

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, codigo, detalle, campos) = ex switch
        {
            ReglaNegocioException r => (Conflictos.Contains(r.Codigo) ? 409 : 400, r.Codigo, r.Message, r.Campos),
            DbUpdateConcurrencyException => (409, "conflicto_concurrencia",
                "Otra persona modificó este registro al mismo tiempo. Recarga y vuelve a intentarlo.", (IReadOnlyList<string>)[]),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (409, "registro_duplicado",
                "Este registro ya existe.", (IReadOnlyList<string>)[]),
            BadHttpRequestException => (400, "solicitud_invalida", "La solicitud no tiene el formato esperado.", (IReadOnlyList<string>)[]),
            _ => (500, "error_interno", "Hay un problema con el servicio. Inténtalo más tarde.", (IReadOnlyList<string>)[]),
        };

        if (status == 500) log.LogError(ex, "Error no controlado en {Ruta}", http.Request.Path);

        http.Response.StatusCode = status;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails =
            {
                Status = status,
                Detail = detalle,
                Extensions = { ["codigo"] = codigo, ["campos"] = campos },
            },
        });
    }
}
