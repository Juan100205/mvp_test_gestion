using System.Net;
using System.Net.Http.Json;
using Sses.Api.Tests.Infra;

namespace Sses.Api.Tests;

/// <summary>CU-06 · HU-01 · RQ_13, RQ_14.</summary>
[Collection(ColeccionApi.Nombre)]
public class AccesoTests(SsesApiFactory api)
{
    [Fact] // Flujo principal 1-2
    public async Task Ingreso_valido_entrega_token_y_perfil()
    {
        var respuesta = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "tutor.caribe", password = SsesApiFactory.Clave });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var sesion = await respuesta.Leer<SesionDto>();
        Assert.False(string.IsNullOrWhiteSpace(sesion.Token));
        Assert.Equal("Tutor", sesion.Usuario.Rol);
        Assert.Equal("Caribe", sesion.Usuario.Region);
    }

    [Fact] // Flujo alterno 1A
    public async Task Clave_incorrecta_niega_el_acceso_e_informa_intentos_restantes()
    {
        var respuesta = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "tutor.pacifico", password = "incorrecta" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var error = await respuesta.Leer<ErrorDto>();
        Assert.Equal("credenciales_invalidas", error.Codigo);
        Assert.Equal(4, error.IntentosRestantes);
    }

    [Fact]
    public async Task Usuario_inexistente_responde_igual_que_clave_incorrecta()
    {
        var respuesta = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "no.existe", password = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("credenciales_invalidas", (await respuesta.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // Flujo alterno 1A: quinto intento
    public async Task Quinto_intento_fallido_bloquea_la_cuenta_aunque_luego_use_la_clave_correcta()
    {
        var cliente = api.CreateClient();
        HttpResponseMessage ultima = null!;
        for (var i = 0; i < 5; i++)
            ultima = await cliente.PostAsJsonAsync("/api/auth/login", new { username = "tutor.bloqueo", password = "mala" });

        Assert.Equal(HttpStatusCode.Locked, ultima.StatusCode);
        var conClaveCorrecta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username = "tutor.bloqueo", password = SsesApiFactory.Clave });
        Assert.Equal(HttpStatusCode.Locked, conClaveCorrecta.StatusCode);
        var error = await conClaveCorrecta.Leer<ErrorDto>();
        Assert.Equal("cuenta_bloqueada", error.Codigo);
        Assert.NotNull(error.BloqueadoHasta);
    }

    [Fact]
    public async Task Sin_token_no_se_accede_a_los_modulos()
    {
        var respuesta = await api.CreateClient().GetAsync("/api/estrategias");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact] // Flujo alterno 2A
    public async Task Funcion_que_no_corresponde_al_perfil_es_restringida()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var ente = await api.ClienteComo("ente.control");

        Assert.Equal(HttpStatusCode.Forbidden, (await tutor.GetAsync("/api/auditoria")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ente.GetAsync("/api/estrategias")).StatusCode);
    }

    [Fact]
    public async Task Yo_devuelve_el_perfil_de_la_sesion()
    {
        var admin = await api.ClienteComo("admin.mineducacion");

        var yo = await (await admin.GetAsync("/api/auth/yo")).Leer<UsuarioDto>();

        Assert.Equal("Administrador", yo.Rol);
    }

    [Fact] // Flujo principal 3-6 (RQ_14)
    public async Task Las_operaciones_quedan_en_la_trazabilidad_y_el_administrador_las_filtra()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var creada = await tutor.PostAsJsonAsync("/api/estrategias",
            new NuevaEstrategia(1, "Trazabilidad", "Prueba de auditoría", "2026-2", [1]));
        var estrategia = await creada.Leer<EstrategiaDto>();

        var admin = await api.ClienteComo("admin.mineducacion");
        var registros = await (await admin.GetAsync("/api/auditoria?usuario=tutor.caribe&operacion=estrategia.crear"))
            .Leer<AuditoriaDto[]>();

        Assert.Contains(registros, r => r.RecursoId == estrategia.Id.ToString() && r.Usuario == "tutor.caribe");
        Assert.All(registros, r => Assert.Equal("estrategia.crear", r.Operacion));
    }

    [Fact] // Flujo alterno 5A
    public async Task Filtros_sin_registros_devuelven_lista_vacia()
    {
        var admin = await api.ClienteComo("admin.mineducacion");

        var registros = await (await admin.GetAsync("/api/auditoria?usuario=nadie.registrado")).Leer<AuditoriaDto[]>();

        Assert.Empty(registros);
    }
}
