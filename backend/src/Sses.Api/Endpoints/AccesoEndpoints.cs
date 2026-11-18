using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sses.Api.Comun;
using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;

namespace Sses.Api.Endpoints;

/// <summary>CU-06: acceso por perfil y trazabilidad.</summary>
public static class AccesoEndpoints
{
    public static void MapAcceso(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Acceso");
        auth.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("login");
        auth.MapGet("/yo", Yo).RequireAuthorization();

        app.MapGet("/api/auditoria", Auditoria).WithTags("Acceso").RequireAuthorization(Politicas.Administrador);
    }

    private static async Task<IResult> Login(LoginRequest req, SsesDbContext db, IPasswordHasher<Usuario> hasher,
        EmisorTokens tokens, Auditor auditor, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var username = req.Username?.Trim().ToLowerInvariant() ?? "";
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Username == username, ct);

        if (usuario is null)
        {
            auditor.Registrar(null, username, "auth.fallido", "usuario", detalle: "usuario inexistente");
            await db.SaveChangesAsync(ct);
            return CredencialesInvalidas(null);
        }

        if (usuario.EstaBloqueado(ahora))
            return CuentaBloqueada(usuario);

        var verificacion = hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, req.Password ?? "");
        if (verificacion == PasswordVerificationResult.Failed)
        {
            usuario.RegistrarIntentoFallido(ahora);
            var bloqueado = usuario.EstaBloqueado(ahora);
            auditor.Registrar(usuario.Id, usuario.Username, bloqueado ? "auth.bloqueo" : "auth.fallido", "usuario", usuario.Id);
            await db.SaveChangesAsync(ct);
            return bloqueado ? CuentaBloqueada(usuario) : CredencialesInvalidas(usuario.IntentosRestantes);
        }

        usuario.RegistrarIngresoExitoso(ahora);
        auditor.Registrar(usuario.Id, usuario.Username, "auth.ingreso", "usuario", usuario.Id);
        await db.SaveChangesAsync(ct);

        var (token, expira) = tokens.Emitir(usuario);
        return Results.Ok(new SesionDto(token, expira, usuario.ADto()));
    }

    private static IResult CredencialesInvalidas(int? intentosRestantes) =>
        Problema.Crear(StatusCodes.Status401Unauthorized, "credenciales_invalidas",
            "El usuario o la contraseña no son correctos.",
            intentosRestantes is null ? null : new Dictionary<string, object?> { ["intentosRestantes"] = intentosRestantes });

    private static IResult CuentaBloqueada(Usuario usuario) =>
        Problema.Crear(StatusCodes.Status423Locked, "cuenta_bloqueada",
            "La cuenta quedó bloqueada temporalmente por intentos fallidos.",
            new Dictionary<string, object?> { ["bloqueadoHasta"] = usuario.BloqueadoHasta });

    private static async Task<IResult> Yo(ClaimsPrincipal user, SsesDbContext db, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == user.UsuarioId(), ct);
        return usuario is null ? Results.Unauthorized() : Results.Ok(usuario.ADto());
    }

    private static async Task<IResult> Auditoria(SsesDbContext db, string? usuario, string? operacion,
        DateTimeOffset? desde, DateTimeOffset? hasta, int? limite, CancellationToken ct)
    {
        var consulta = db.Auditoria.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(usuario)) consulta = consulta.Where(a => a.Usuario == usuario.Trim().ToLower());
        if (!string.IsNullOrWhiteSpace(operacion)) consulta = consulta.Where(a => a.Operacion == operacion.Trim());
        if (desde is not null) consulta = consulta.Where(a => a.Fecha >= desde);
        if (hasta is not null) consulta = consulta.Where(a => a.Fecha <= hasta);

        var registros = await consulta
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .Take(Math.Clamp(limite ?? 200, 1, 1000))
            .Select(a => new AuditoriaDto(a.Id, a.Usuario, a.Operacion, a.Recurso, a.RecursoId, a.Detalle, a.Fecha))
            .ToListAsync(ct);
        return Results.Ok(registros);
    }
}
