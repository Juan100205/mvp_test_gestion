using System.Net;
using System.Net.Http.Json;
using Sses.Api.Tests.Infra;

namespace Sses.Api.Tests;

/// <summary>CU-03 (HU-05, HU-06, HU-07) · RQ_06, RQ_07, RQ_08, RQ_21.</summary>
[Collection(ColeccionApi.Nombre)]
public class AcompanamientosTests(SsesApiFactory api)
{
    private static async Task<EstrategiaDto> NuevaEstrategia(HttpClient tutor)
    {
        var respuesta = await tutor.PostAsJsonAsync("/api/estrategias",
            new NuevaEstrategia(1, "Matemáticas en contexto", "Proyectos de aula", "2026-2", [1, 2, 3]));
        return await respuesta.Leer<EstrategiaDto>();
    }

    private static object Individual(int estrategiaId, Guid? id = null, int docente = 1) => new
    {
        id = id ?? Guid.NewGuid(),
        estrategiaId,
        tipo = "Individual",
        periodo = "2026-11",
        objetivo = "Fortalecer la planeación de clase",
        compromisos = "Entregar la planeación ajustada",
        docenteIds = new[] { docente },
    };

    [Fact] // CU-03 2: docentes asociados a la estrategia
    public async Task La_estrategia_muestra_sus_docentes_asociados()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);

        Assert.Equal(3, estrategia.Docentes.Length);
    }

    [Fact] // CU-03 3-4
    public async Task Registrar_acompanamiento_mensual_individual()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);

        var respuesta = await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var acompanamiento = await respuesta.Leer<AcompanamientoDto>();
        Assert.Equal("Individual", acompanamiento.Tipo);
        Assert.Equal("2026-11", acompanamiento.Periodo);
        Assert.False(acompanamiento.Existente);
    }

    [Fact]
    public async Task Registrar_acompanamiento_grupal()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);

        var respuesta = await tutor.PostAsJsonAsync("/api/acompanamientos", new
        {
            id = Guid.NewGuid(), estrategiaId = estrategia.Id, tipo = "Grupal", periodo = "2026-11",
            objetivo = "Comunidad de práctica", compromisos = "", docenteIds = new[] { 2, 3 },
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(2, (await respuesta.Leer<AcompanamientoDto>()).Docentes.Length);
    }

    [Fact] // RQ_06: solo docentes asociados
    public async Task Docente_no_asociado_es_rechazado()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);

        var respuesta = await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id, docente: 4));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("docente_no_asociado", (await respuesta.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // CU-03 4A
    public async Task Si_el_docente_ya_tiene_acompanamiento_en_el_mes_se_muestra_el_existente()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);
        var primero = await (await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id))).Leer<AcompanamientoDto>();

        var respuesta = await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var existente = await respuesta.Leer<AcompanamientoDto>();
        Assert.True(existente.Existente);
        Assert.Equal(primero.Id, existente.Id);
    }

    [Fact] // RQ_21: reenvío desde la cola sin conexión con el mismo Id
    public async Task Reenviar_el_mismo_registro_sin_conexion_no_lo_duplica()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);
        var id = Guid.NewGuid();

        await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id, id));
        var reenvio = await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id, id));

        Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);
        Assert.Equal(id, (await reenvio.Leer<AcompanamientoDto>()).Id);
    }

    [Fact] // CU-03 5 y 5A
    public async Task El_seguimiento_semanal_se_guarda_y_la_misma_semana_se_actualiza()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);
        var acompanamiento = await (await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id))).Leer<AcompanamientoDto>();
        var url = $"/api/acompanamientos/{acompanamiento.Id}/seguimientos/2";

        var primero = await tutor.PutAsJsonAsync(url, new { id = Guid.NewGuid(), actividades = "Observación de clase", avance = 30, observaciones = "" });
        var segundo = await tutor.PutAsJsonAsync(url, new { id = Guid.NewGuid(), actividades = "Observación y retroalimentación", avance = 55, observaciones = "Mejoró la gestión del tiempo" });

        Assert.Equal(HttpStatusCode.OK, primero.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segundo.StatusCode);
        var actualizado = await segundo.Leer<AcompanamientoDto>();
        Assert.Single(actualizado.Seguimientos);
        Assert.Equal(55, actualizado.AvanceActual);
    }

    [Fact]
    public async Task Semana_fuera_de_rango_es_rechazada()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);
        var acompanamiento = await (await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id))).Leer<AcompanamientoDto>();

        var respuesta = await tutor.PutAsJsonAsync($"/api/acompanamientos/{acompanamiento.Id}/seguimientos/9",
            new { id = Guid.NewGuid(), actividades = "x", avance = 10, observaciones = "" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("seguimiento_invalido", (await respuesta.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // CU-03 7 · HU-07 · RQ_08
    public async Task Historial_por_docente_incluye_acompanamientos_individuales_y_grupales()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await NuevaEstrategia(tutor);
        await tutor.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id, docente: 2));
        await tutor.PostAsJsonAsync("/api/acompanamientos", new
        {
            id = Guid.NewGuid(), estrategiaId = estrategia.Id, tipo = "Grupal", periodo = "2026-10",
            objetivo = "Comunidad de práctica", compromisos = "", docenteIds = new[] { 2, 3 },
        });

        var porDocente = await (await tutor.GetAsync($"/api/estrategias/{estrategia.Id}/acompanamientos?docenteId=2")).Leer<AcompanamientoDto[]>();
        var porDocente3 = await (await tutor.GetAsync($"/api/estrategias/{estrategia.Id}/acompanamientos?docenteId=3")).Leer<AcompanamientoDto[]>();

        Assert.Equal(2, porDocente.Length);
        Assert.Equal("2026-11", porDocente[0].Periodo); // más reciente primero
        Assert.Single(porDocente3);
    }

    [Fact]
    public async Task Otro_tutor_no_puede_registrar_en_una_estrategia_ajena()
    {
        var caribe = await api.ClienteComo("tutor.caribe");
        var pacifico = await api.ClienteComo("tutor.pacifico");
        var estrategia = await NuevaEstrategia(caribe);

        var respuesta = await pacifico.PostAsJsonAsync("/api/acompanamientos", Individual(estrategia.Id));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
