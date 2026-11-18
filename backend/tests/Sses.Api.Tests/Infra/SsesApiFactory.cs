using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Sses.Api.Tests.Infra;

/// <summary>
/// Levanta la API completa contra un PostgreSQL real en contenedor (Testcontainers),
/// con migraciones y datos precargados, igual que en docker compose.
/// </summary>
public sealed class SsesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Clave = "Piloto2026*";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("sses")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Sses", _db.GetConnectionString());
        builder.UseSetting("Sses:InicializarBaseDeDatos", "true");
        builder.UseSetting("Sses:LoginPorMinuto", "10000");
        builder.UseSetting("Jwt:Clave", "clave-de-pruebas-de-integracion-con-32-caracteres-o-mas");
    }

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    public async Task<HttpClient> ClienteComo(string usuario)
    {
        var cliente = CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { username = usuario, password = Clave });
        respuesta.EnsureSuccessStatusCode();
        var sesion = await respuesta.Content.ReadFromJsonAsync<SesionDto>(Json.Opciones);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.Token);
        return cliente;
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<SsesApiFactory>
{
    public const string Nombre = "api";
}
