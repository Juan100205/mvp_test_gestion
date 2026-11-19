using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sses.Api.Comun;
using Sses.Api.Endpoints;
using Sses.Api.Seguridad;
using Sses.Domain;
using Sses.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Datos
builder.Services.AddDbContext<SsesDbContext>(o =>
    o.UseNpgsql(CadenaConexion.Normalizar(builder.Configuration.GetConnectionString("Sses")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'Sses'."))));

// Seguridad (RQ_13, RQ_20)
var jwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
if (jwt.Clave.Length < 32)
    throw new InvalidOperationException("Jwt:Clave debe tener al menos 32 caracteres.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EmisorTokens>();
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emisor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = jwt.LlaveFirma(),
            RoleClaimType = ClaimsSses.Rol,
            NameClaimType = ClaimsSses.Usuario,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Tutor, p => p.RequireRole(Politicas.Tutor))
    .AddPolicy(Politicas.Administrador, p => p.RequireRole(Politicas.Administrador));

// Protección contra fuerza bruta además del bloqueo por cuenta.
var loginPorMinuto = builder.Configuration.GetValue("Sses:LoginPorMinuto", 30);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

// API
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Auditor>();
builder.Services.AddOpenApi();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Sses:InicializarBaseDeDatos"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SsesDbContext>();
    await DatosPrecargados.InicializarAsync(db);
    if (app.Configuration.GetValue<bool>("Sses:CargarDatosDemo"))
        await DatosDemo.CargarAsync(db, TimeProvider.System.GetUtcNow());
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (!app.Environment.IsProduction()) app.MapOpenApi();

app.MapGet("/health", async (SsesDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct) ? Results.Ok(new { estado = "ok" }) : Results.StatusCode(503))
    .AllowAnonymous().ExcludeFromDescription();

app.MapAcceso();
app.MapEstrategias();
app.MapAcompanamientos();

app.Run();

public partial class Program;
