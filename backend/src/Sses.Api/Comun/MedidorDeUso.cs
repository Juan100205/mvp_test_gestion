using System.Diagnostics;
using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;

namespace Sses.Api.Comun;

/// <summary>
/// CU-07 paso 4 (RQ_15): registra el uso y el tiempo de respuesta de cada petición de un tutor al API.
/// Guarda la plantilla de la ruta (sin ids ni datos) y nunca hace fallar la petición si la métrica no se guarda.
/// </summary>
public sealed class MedidorDeUso(RequestDelegate siguiente, IServiceScopeFactory alcances, TimeProvider reloj, ILogger<MedidorDeUso> log)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var reloj0 = Stopwatch.GetTimestamp();
        await siguiente(http);

        var usuario = http.User;
        if (!http.Request.Path.StartsWithSegments("/api") || usuario.Identity?.IsAuthenticated != true
            || usuario.FindFirst(ClaimsSses.Rol)?.Value != nameof(Rol.Tutor))
            return;

        try
        {
            var ruta = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value ?? "";
            var milisegundos = (int)Stopwatch.GetElapsedTime(reloj0).TotalMilliseconds;
            await using var alcance = alcances.CreateAsyncScope();
            var db = alcance.ServiceProvider.GetRequiredService<SsesDbContext>();
            db.Metricas.Add(new MetricaPeticion(usuario.UsuarioId(), http.Request.Method, Formato.Recortar(ruta, 200),
                http.Response.StatusCode, milisegundos, reloj.GetUtcNow()));
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "No se pudo registrar la métrica de uso");
        }
    }
}
