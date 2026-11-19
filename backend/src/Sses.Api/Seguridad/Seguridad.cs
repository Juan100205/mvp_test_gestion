using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Sses.Domain;

namespace Sses.Api.Seguridad;

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";
    public string Emisor { get; set; } = "sses-piloto";
    public string Audiencia { get; set; } = "sses-portal";
    public string Clave { get; set; } = "";
    public int HorasVigencia { get; set; } = 8;

    public SymmetricSecurityKey LlaveFirma() => new(Encoding.UTF8.GetBytes(Clave));
}

public static class Politicas
{
    public const string Tutor = nameof(Rol.Tutor);
    public const string Administrador = nameof(Rol.Administrador);

    public const string LiderPilotaje = nameof(Rol.LiderPilotaje);

    /// <summary>CU-04 y CU-05: consulta y descarga de reportes (funcionario, ente de control y administrador).</summary>
    public const string Reportes = "Reportes";
}

public static class ClaimsSses
{
    public const string Rol = "role";
    public const string Usuario = "unique_name";
    public const string Region = "region";

    public static int UsuarioId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public static string Username(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(Usuario) ?? "anonimo";

    public static Sses.Domain.Rol RolUsuario(this ClaimsPrincipal principal) =>
        Enum.Parse<Sses.Domain.Rol>(principal.FindFirstValue(Rol)!);
}

public sealed class EmisorTokens(OpcionesJwt opciones, TimeProvider reloj)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset Expira) Emitir(Usuario usuario)
    {
        var expira = reloj.GetUtcNow().AddHours(opciones.HorasVigencia);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = opciones.Emisor,
            Audience = opciones.Audiencia,
            Expires = expira.UtcDateTime,
            IssuedAt = reloj.GetUtcNow().UtcDateTime,
            NotBefore = reloj.GetUtcNow().UtcDateTime,
            SigningCredentials = new SigningCredentials(opciones.LlaveFirma(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                [ClaimsSses.Usuario] = usuario.Username,
                [JwtRegisteredClaimNames.Name] = usuario.Nombre,
                [ClaimsSses.Rol] = usuario.Rol.ToString(),
                [ClaimsSses.Region] = usuario.Region,
            },
        });
        return (token, expira);
    }
}
