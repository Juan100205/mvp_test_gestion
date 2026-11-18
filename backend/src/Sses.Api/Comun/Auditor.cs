using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;

namespace Sses.Api.Comun;

/// <summary>
/// RQ_14: agrega el registro de auditoría a la misma unidad de trabajo,
/// así la operación y su rastro se guardan juntos o no se guarda ninguno.
/// </summary>
public sealed class Auditor(SsesDbContext db, IHttpContextAccessor http, TimeProvider reloj)
{
    public void Registrar(string operacion, string recurso, object? recursoId = null, string? detalle = null)
    {
        var usuario = http.HttpContext?.User;
        var autenticado = usuario?.Identity?.IsAuthenticated == true;
        Registrar(autenticado ? usuario!.UsuarioId() : null, autenticado ? usuario!.Username() : "anonimo",
            operacion, recurso, recursoId, detalle);
    }

    public void Registrar(int? usuarioId, string username, string operacion, string recurso, object? recursoId = null, string? detalle = null) =>
        db.Auditoria.Add(new RegistroAuditoria(usuarioId, username, operacion, recurso, recursoId?.ToString(), detalle, reloj.GetUtcNow()));
}
