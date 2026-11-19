using System.Net;
using System.Net.Http.Json;
using Sses.Api.Tests.Infra;

namespace Sses.Api.Tests;

/// <summary>CU-07 (HU-11, HU-12) · RQ_14, RQ_15, RQ_16.</summary>
[Collection(ColeccionApi.Nombre)]
public class SesionesTests(SsesApiFactory api)
{
    private const int TutorCaribe = 1;
    private const int TutorPacifico = 2;

    private static async Task<SesionDetalleDto> Programar(HttpClient lider, string region, int[] tutores, int minutosDesdeAhora = 10,
        string nombre = "Sesión de prueba")
    {
        var respuesta = await lider.PostAsJsonAsync("/api/sesiones", new
        {
            nombre,
            region,
            inicio = DateTimeOffset.UtcNow.AddMinutes(minutosDesdeAhora),
            duracionMinutos = 120,
            tutorIds = tutores,
        });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.Leer<SesionDetalleDto>();
    }

    private static object Encuesta(params int?[] respuestas) => new { respuestas, comentario = "Funcionó sin señal" };

    [Fact] // CU-07 pasos 1-2
    public async Task El_lider_programa_una_sesion_y_el_tutor_convocado_la_ve()
    {
        var lider = await api.ClienteComo("lider.piloto");
        var sesion = await Programar(lider, "Caribe", [TutorCaribe], nombre: "Sesión 1 Caribe");

        var tutor = await api.ClienteComo("tutor.caribe");
        var mias = await (await tutor.GetAsync("/api/mis-sesiones")).Leer<MiSesionDto[]>();

        var mia = Assert.Single(mias, s => s.Id == sesion.Id);
        Assert.Equal("Programada", mia.Estado);
        Assert.True(mia.PuedeIngresar);
        Assert.Null(mia.IngresoEn);
        var listado = await (await lider.GetAsync("/api/sesiones")).Leer<SesionResumenDto[]>();
        Assert.Contains(listado, s => s.Id == sesion.Id && s.Convocados == 1);
    }

    [Fact]
    public async Task El_lider_solo_puede_convocar_tutores_de_la_region()
    {
        var lider = await api.ClienteComo("lider.piloto");

        var elegibles = await (await lider.GetAsync("/api/sesiones/tutores?region=Pacífico")).Leer<TutorReporteDto[]>();
        var respuesta = await lider.PostAsJsonAsync("/api/sesiones", new
        {
            nombre = "Mezcla de regiones",
            region = "Caribe",
            inicio = DateTimeOffset.UtcNow.AddHours(1),
            duracionMinutos = 60,
            tutorIds = new[] { TutorCaribe, TutorPacifico },
        });

        Assert.Equal(["Andrés Mosquera Rentería"], elegibles.Select(t => t.Nombre));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("tutor_de_otra_region", (await respuesta.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // CU-06 2A
    public async Task Solo_el_lider_programa_y_consulta_sesiones()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var ente = await api.ClienteComo("ente.control");

        Assert.Equal(HttpStatusCode.Forbidden, (await tutor.GetAsync("/api/sesiones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ente.GetAsync("/api/sesiones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ente.GetAsync("/api/mis-sesiones")).StatusCode);
    }

    [Fact] // CU-07 paso 3
    public async Task El_tutor_convocado_registra_su_asistencia_una_sola_vez()
    {
        var sesion = await Programar(await api.ClienteComo("lider.piloto"), "Caribe", [TutorCaribe]);
        var tutor = await api.ClienteComo("tutor.caribe");

        var primera = await (await tutor.PostAsync($"/api/sesiones/{sesion.Id}/asistencia", null)).Leer<MiSesionDto>();
        var segunda = await (await tutor.PostAsync($"/api/sesiones/{sesion.Id}/asistencia", null)).Leer<MiSesionDto>();

        Assert.NotNull(primera.IngresoEn);
        Assert.Equal(primera.IngresoEn, segunda.IngresoEn);
    }

    [Fact] // Flujo alterno 3A
    public async Task Un_tutor_no_convocado_recibe_su_sesion_asignada()
    {
        var lider = await api.ClienteComo("lider.piloto");
        var caribe = await Programar(lider, "Caribe", [TutorCaribe]);
        var asignada = await Programar(lider, "Pacífico", [TutorPacifico], minutosDesdeAhora: 60 * 24 * 300, nombre: "Sesión asignada Pacífico");
        var pacifico = await api.ClienteComo("tutor.pacifico");

        var respuesta = await pacifico.PostAsync($"/api/sesiones/{caribe.Id}/asistencia", null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        var error = await respuesta.Leer<ErrorDto>();
        Assert.Equal("no_convocado", error.Codigo);
        Assert.NotNull(error.SesionAsignada);
        Assert.Contains("Tu sesión asignada es", error.Detail);
        var mias = await (await pacifico.GetAsync("/api/mis-sesiones")).Leer<MiSesionDto[]>();
        Assert.Contains(mias, s => s.Id == asignada.Id);
        Assert.DoesNotContain(mias, s => s.Id == caribe.Id);
    }

    [Fact]
    public async Task Fuera_del_horario_no_se_registra_la_asistencia()
    {
        var sesion = await Programar(await api.ClienteComo("lider.piloto"), "Caribe", [TutorCaribe], minutosDesdeAhora: 180);
        var tutor = await api.ClienteComo("tutor.caribe");

        var respuesta = await tutor.PostAsync($"/api/sesiones/{sesion.Id}/asistencia", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("sesion_fuera_de_horario", (await respuesta.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // CU-07 paso 5 y flujo alterno 5A
    public async Task La_encuesta_valida_las_preguntas_pendientes_y_se_responde_una_vez()
    {
        var sesion = await Programar(await api.ClienteComo("lider.piloto"), "Caribe", [TutorCaribe]);
        var tutor = await api.ClienteComo("tutor.caribe");
        var preguntas = await (await tutor.GetAsync("/api/sesiones/preguntas")).Leer<PreguntaDto[]>();
        await tutor.PostAsync($"/api/sesiones/{sesion.Id}/asistencia", null);

        var incompleta = await tutor.PostAsJsonAsync($"/api/sesiones/{sesion.Id}/encuesta", Encuesta(5, null, 4, 4, 5));
        var completa = await tutor.PostAsJsonAsync($"/api/sesiones/{sesion.Id}/encuesta", Encuesta(5, 4, 4, 4, 5));
        var repetida = await tutor.PostAsJsonAsync($"/api/sesiones/{sesion.Id}/encuesta", Encuesta(5, 4, 4, 4, 5));

        Assert.Equal(5, preguntas.Length);
        Assert.Equal(HttpStatusCode.BadRequest, incompleta.StatusCode);
        Assert.Equal(["p2"], (await incompleta.Leer<ErrorDto>()).Campos);
        Assert.Equal(HttpStatusCode.Created, completa.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        var mia = Assert.Single(await (await tutor.GetAsync("/api/mis-sesiones")).Leer<MiSesionDto[]>(), s => s.Id == sesion.Id);
        Assert.True(mia.EncuestaRespondida);
    }

    [Fact] // CU-07 pasos 4 y 6: asistencia, uso, incidentes y encuesta en el consolidado
    public async Task El_consolidado_presenta_asistencia_uso_incidentes_y_encuesta()
    {
        var lider = await api.ClienteComo("lider.piloto");
        var sesion = await Programar(lider, "Caribe", [TutorCaribe], nombre: "Sesión consolidada");
        var tutor = await api.ClienteComo("tutor.caribe");
        await tutor.PostAsync($"/api/sesiones/{sesion.Id}/asistencia", null);
        await tutor.GetAsync("/api/estrategias"); // uso durante la sesión
        await tutor.PostAsJsonAsync($"/api/sesiones/{sesion.Id}/encuesta", Encuesta(5, 4, 3, 4, 5));
        await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "tutor.caribe", password = "errada" }); // incidente automático
        await api.ClienteComo("tutor.caribe"); // reinicia los intentos
        var manual = await lider.PostAsJsonAsync($"/api/sesiones/{sesion.Id}/incidentes", new { descripcion = "Se cayó el wifi de la sede" });

        var doc = await (await lider.GetAsync($"/api/sesiones/{sesion.Id}/consolidado")).Leer<ReporteDocumentoDto>();

        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);
        Assert.Equal("Sesión consolidada", doc.Titulo);
        Assert.Contains(doc.Cifras, c => c.Etiqueta == "asistencia" && c.Valor == "100 %");
        var asistencia = Assert.Single(doc.Tablas.Single(t => t.Titulo == "Asistencia y uso del sistema").Filas);
        Assert.Equal("Laura Gómez Palacios", asistencia[0]);
        Assert.Equal("Sí", asistencia[1]);
        Assert.True(int.Parse(asistencia[4]) >= 2, "registra las peticiones del tutor durante la sesión");
        var incidentes = doc.Tablas.Single(t => t.Titulo == "Incidentes").Filas;
        Assert.Contains(incidentes, f => f[1] == "Manual" && f[2] == "Se cayó el wifi de la sede");
        Assert.Contains(incidentes, f => f[1] == "Automático" && f[2].Contains("Intento de ingreso fallido"));
        var encuesta = doc.Tablas.Single(t => t.Titulo == "Encuesta de percepción").Filas;
        Assert.Equal("3.0", encuesta[2][2]);
        Assert.Contains(doc.Tablas.Single(t => t.Titulo == "Comentarios").Filas, f => f[0] == "Funcionó sin señal");
    }

    [Fact] // CU-07 paso 7 · RQ_14
    public async Task Programar_asistir_responder_y_exportar_quedan_en_la_trazabilidad()
    {
        var lider = await api.ClienteComo("lider.piloto");
        var sesion = await Programar(lider, "Caribe", [TutorCaribe]);
        var tutor = await api.ClienteComo("tutor.caribe");
        await tutor.PostAsync($"/api/sesiones/{sesion.Id}/asistencia", null);
        await tutor.PostAsJsonAsync($"/api/sesiones/{sesion.Id}/encuesta", Encuesta(4, 4, 4, 4, 4));

        var exportar = await lider.PostAsJsonAsync($"/api/sesiones/{sesion.Id}/descargas", new { formato = "csv" });

        Assert.Equal(HttpStatusCode.NoContent, exportar.StatusCode);
        var admin = await api.ClienteComo("admin.mineducacion");
        var id = sesion.Id.ToString();
        foreach (var operacion in new[] { "sesion.programar", "sesion.asistencia", "sesion.encuesta", "sesion.exportar" })
        {
            var registros = await (await admin.GetAsync($"/api/auditoria?operacion={operacion}")).Leer<AuditoriaDto[]>();
            Assert.Contains(registros, r => r.RecursoId == id);
        }
    }
}
