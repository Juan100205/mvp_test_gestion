using System.Net;
using System.Net.Http.Json;
using Sses.Api.Tests.Infra;

namespace Sses.Api.Tests;

/// <summary>CU-01 (HU-02, HU-03) y CU-02 (HU-04) · RQ_01 a RQ_05.</summary>
[Collection(ColeccionApi.Nombre)]
public class EstrategiasTests(SsesApiFactory api)
{
    private static NuevaEstrategia Valida(string nombre = "Club de lectura") =>
        new(1, nombre, "Lectura en voz alta los viernes", "2026-2", [1, 2]);

    private static async Task<EstrategiaDto> Crear(HttpClient tutor, NuevaEstrategia? datos = null)
    {
        var respuesta = await tutor.PostAsJsonAsync("/api/estrategias", datos ?? Valida());
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.Leer<EstrategiaDto>();
    }

    [Fact] // CU-01 paso 3: lista de entidades previamente definidas
    public async Task El_formulario_ofrece_entidades_y_sus_docentes()
    {
        var tutor = await api.ClienteComo("tutor.caribe");

        var entidades = await (await tutor.GetAsync("/api/entidades")).Leer<EntidadDto[]>();
        var docentes = await (await tutor.GetAsync($"/api/entidades/{entidades[0].Id}/docentes")).Leer<DocenteDto[]>();

        Assert.NotEmpty(entidades);
        Assert.NotEmpty(docentes);
    }

    [Fact] // CU-01 pasos 4-5
    public async Task Crear_estrategia_la_guarda_editable_y_asociada_a_la_entidad()
    {
        var tutor = await api.ClienteComo("tutor.caribe");

        var respuesta = await tutor.PostAsJsonAsync("/api/estrategias", Valida());

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.NotNull(respuesta.Headers.Location);
        var estrategia = await respuesta.Leer<EstrategiaDto>();
        Assert.Equal("Editable", estrategia.Estado);
        Assert.Equal(1, estrategia.Entidad.Id);
        Assert.Equal(2, estrategia.Docentes.Length);
    }

    [Fact] // CU-01 4A: datos incompletos
    public async Task Datos_incompletos_informan_que_corregir()
    {
        var tutor = await api.ClienteComo("tutor.caribe");

        var respuesta = await tutor.PostAsJsonAsync("/api/estrategias", Valida() with { Nombre = "", Periodo = " " });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Leer<ErrorDto>();
        Assert.Equal("datos_incompletos", error.Codigo);
        Assert.Contains("nombre", error.Campos!);
        Assert.Contains("periodo", error.Campos!);
    }

    [Fact] // CU-01 4A: entidad no válida
    public async Task Entidad_inexistente_es_rechazada()
    {
        var tutor = await api.ClienteComo("tutor.caribe");

        var respuesta = await tutor.PostAsJsonAsync("/api/estrategias", Valida() with { EntidadId = 999_999 });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("entidad_no_valida", (await respuesta.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // CU-01 2: solo las estrategias del tutor
    public async Task El_tutor_ve_sus_estrategias_y_no_las_de_otros()
    {
        var caribe = await api.ClienteComo("tutor.caribe");
        var pacifico = await api.ClienteComo("tutor.pacifico");
        var propia = await Crear(caribe, Valida("Solo del Caribe"));

        var listado = await (await caribe.GetAsync("/api/estrategias")).Leer<EstrategiaResumenDto[]>();
        var ajena = await pacifico.GetAsync($"/api/estrategias/{propia.Id}");

        Assert.Contains(listado, e => e.Id == propia.Id);
        Assert.Equal(HttpStatusCode.NotFound, ajena.StatusCode);
    }

    [Fact] // CU-01 6-7
    public async Task Editar_sin_estudiantes_actualiza()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await Crear(tutor);

        var respuesta = await tutor.PutAsJsonAsync($"/api/estrategias/{estrategia.Id}", Valida("Club de lectura crítica"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Club de lectura crítica", (await respuesta.Leer<EstrategiaDto>()).Nombre);
    }

    [Fact] // CU-02 3: búsqueda por documento en la base precargada
    public async Task Buscar_estudiante_por_documento()
    {
        var tutor = await api.ClienteComo("tutor.caribe");

        var encontrado = await tutor.GetAsync("/api/estudiantes?documento=1100000001");
        var inexistente = await tutor.GetAsync("/api/estudiantes?documento=0000000000");

        Assert.Equal(HttpStatusCode.OK, encontrado.StatusCode);
        Assert.Equal("1100000001", (await encontrado.Leer<EstudianteDto>()).NumeroDocumento);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal("estudiante_no_encontrado", (await inexistente.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // CU-02 4-7 y CU-01 6A
    public async Task Al_vincular_el_primer_estudiante_la_estrategia_queda_solo_consulta()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await Crear(tutor);

        var vinculo = await tutor.PostAsJsonAsync($"/api/estrategias/{estrategia.Id}/estudiantes", new { estudianteId = 1 });
        var edicion = await tutor.PutAsJsonAsync($"/api/estrategias/{estrategia.Id}", Valida("Intento de edición"));
        var detalle = await (await tutor.GetAsync($"/api/estrategias/{estrategia.Id}")).Leer<EstrategiaDto>();

        Assert.Equal(HttpStatusCode.Created, vinculo.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, edicion.StatusCode);
        Assert.Equal("estrategia_bloqueada", (await edicion.Leer<ErrorDto>()).Codigo);
        Assert.Equal("SoloConsulta", detalle.Estado);
        Assert.False(detalle.EsEditable);
        Assert.Single(detalle.Estudiantes);
    }

    [Fact] // RQ_05
    public async Task Vincular_dos_veces_al_mismo_estudiante_es_rechazado()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await Crear(tutor);
        await tutor.PostAsJsonAsync($"/api/estrategias/{estrategia.Id}/estudiantes", new { estudianteId = 2 });

        var repetido = await tutor.PostAsJsonAsync($"/api/estrategias/{estrategia.Id}/estudiantes", new { estudianteId = 2 });

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("estudiante_ya_vinculado", (await repetido.Leer<ErrorDto>()).Codigo);
    }

    [Fact] // RQ_04: no se crean estudiantes nuevos
    public async Task Vincular_un_estudiante_inexistente_responde_no_encontrado()
    {
        var tutor = await api.ClienteComo("tutor.caribe");
        var estrategia = await Crear(tutor);

        var respuesta = await tutor.PostAsJsonAsync($"/api/estrategias/{estrategia.Id}/estudiantes", new { estudianteId = 999_999 });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
