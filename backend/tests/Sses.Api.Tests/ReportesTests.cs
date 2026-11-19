using System.Net;
using System.Net.Http.Json;
using Sses.Api.Tests.Infra;

namespace Sses.Api.Tests;

/// <summary>CU-04 (HU-08, HU-09) y CU-05 (HU-10) · RQ_09, RQ_10, RQ_11, RQ_12, RQ_14.</summary>
[Collection(ColeccionApi.Nombre)]
public class ReportesTests(SsesApiFactory api)
{
    /// <summary>Estrategia en el Pacífico con un estudiante y un acompañamiento de noviembre con avance del 40 %.</summary>
    private static async Task<EstrategiaDto> EstrategiaConActividad(HttpClient tutor, string nombre)
    {
        var estrategia = await (await tutor.PostAsJsonAsync("/api/estrategias",
            new NuevaEstrategia(4, nombre, "Para reportes", "2026-2", [13, 14]))).Leer<EstrategiaDto>();
        (await tutor.PostAsJsonAsync($"/api/estrategias/{estrategia.Id}/estudiantes", new { estudianteId = 4 })).EnsureSuccessStatusCode();

        var acompanamiento = await (await tutor.PostAsJsonAsync("/api/acompanamientos", new
        {
            id = Guid.NewGuid(),
            estrategiaId = estrategia.Id,
            tipo = "Individual",
            periodo = "2026-11",
            objetivo = "Planeación de clase",
            compromisos = "",
            docenteIds = new[] { 13 },
        })).Leer<AcompanamientoDto>();
        (await tutor.PutAsJsonAsync($"/api/acompanamientos/{acompanamiento.Id}/seguimientos/1",
            new { id = Guid.NewGuid(), actividades = "Revisión del planeador", avance = 40, observaciones = "" })).EnsureSuccessStatusCode();
        return estrategia;
    }

    private static string[] Fila(ReporteDocumentoDto doc, string tabla, string contiene) =>
        Assert.Single(doc.Tablas.Single(t => t.Titulo == tabla).Filas, f => f.Contains(contiene));

    private static string Celda(ReporteDocumentoDto doc, string tabla, string[] fila, string columna) =>
        fila[Array.IndexOf(doc.Tablas.Single(t => t.Titulo == tabla).Encabezado, columna)];

    // ---------- CU-04 / CU-05 paso 2: reportes autorizados por perfil ----------

    [Fact]
    public async Task El_funcionario_ve_desempeno_actividades_y_reportes_de_ley()
    {
        var funcionario = await api.ClienteComo("funcionario.men");

        var tipos = await (await funcionario.GetAsync("/api/reportes/tipos")).Leer<TipoReporteDto[]>();

        Assert.Equal(["consolidado", "desempeno", "actividades", "ley-cobertura", "ley-datos-personales"], tipos.Select(t => t.Id));
        Assert.Contains("tutorId", tipos.Single(t => t.Id == "desempeno").Filtros);
        Assert.Equal(["periodo"], tipos.Single(t => t.Id == "ley-cobertura").Requeridos);
    }

    [Fact] // CU-05: perfil de solo consulta con criterios autorizados
    public async Task El_ente_de_control_solo_ve_el_consolidado_con_periodo_entidad_y_region()
    {
        var ente = await api.ClienteComo("ente.control");

        var tipo = Assert.Single(await (await ente.GetAsync("/api/reportes/tipos")).Leer<TipoReporteDto[]>());

        Assert.Equal("consolidado", tipo.Id);
        Assert.Equal(["region", "entidadId", "periodo", "mes"], tipo.Filtros);
    }

    [Fact] // CU-06 2A
    public async Task El_tutor_no_accede_a_los_reportes()
    {
        var tutor = await api.ClienteComo("tutor.caribe");

        Assert.Equal(HttpStatusCode.Forbidden, (await tutor.GetAsync("/api/reportes/consolidado")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tutor.GetAsync("/api/reportes/tipos")).StatusCode);
    }

    [Fact] // CU-05 flujo alterno 3A
    public async Task Criterios_fuera_de_los_permisos_del_ente_se_restringen_e_informan_lo_autorizado()
    {
        var ente = await api.ClienteComo("ente.control");

        var otroTipo = await ente.GetAsync("/api/reportes/desempeno");
        var otroFiltro = await ente.GetAsync("/api/reportes/consolidado?tutorId=2");

        Assert.Equal(HttpStatusCode.Forbidden, otroTipo.StatusCode);
        var error = await otroTipo.Leer<ErrorDto>();
        Assert.Equal("criterio_no_autorizado", error.Codigo);
        Assert.Contains("Estrategias y acompañamiento", error.Detail);
        Assert.Equal(HttpStatusCode.Forbidden, otroFiltro.StatusCode);
        Assert.Equal(["tutorId"], (await otroFiltro.Leer<ErrorDto>()).Campos);
    }

    // ---------- CU-04 flujo alterno 3A: filtros no válidos ----------

    [Theory]
    [InlineData("/api/reportes/desempeno?periodo=2026-7", "periodo")]
    [InlineData("/api/reportes/desempeno?mes=noviembre", "mes")]
    [InlineData("/api/reportes/desempeno?region=Caribe&entidadId=4", "entidadId")]
    [InlineData("/api/reportes/desempeno?tutorId=7", "tutorId")]
    [InlineData("/api/reportes/ley-cobertura", "periodo")]
    public async Task Filtros_no_validos_o_incompletos_indican_que_corregir(string url, string campo)
    {
        var funcionario = await api.ClienteComo("funcionario.men");

        var respuesta = await funcionario.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Leer<ErrorDto>();
        Assert.Equal("filtros_no_validos", error.Codigo);
        Assert.Equal([campo], error.Campos);
    }

    [Fact]
    public async Task Tipo_de_reporte_inexistente_responde_404()
    {
        var funcionario = await api.ClienteComo("funcionario.men");

        Assert.Equal(HttpStatusCode.NotFound, (await funcionario.GetAsync("/api/reportes/no-existe")).StatusCode);
    }

    // ---------- Contenido de cada reporte ----------

    [Fact] // CU-05 pasos 4-5 · RQ_11
    public async Task El_consolidado_muestra_cada_estrategia_con_fecha_de_corte()
    {
        var estrategia = await EstrategiaConActividad(await api.ClienteComo("tutor.pacifico"), "Consolidado Pacífico");
        var ente = await api.ClienteComo("ente.control");

        var doc = await (await ente.GetAsync("/api/reportes/consolidado?region=Pacífico")).Leer<ReporteDocumentoDto>();

        Assert.Equal("Región: Pacífico", doc.Filtros);
        Assert.False(doc.SinDatos);
        var fila = Fila(doc, "Estrategias", estrategia.Nombre);
        Assert.Equal("IE Normal Superior de Quibdó", Celda(doc, "Estrategias", fila, "Entidad"));
        Assert.Equal("Solo consulta", Celda(doc, "Estrategias", fila, "Estado"));
        Assert.Equal("1", Celda(doc, "Estrategias", fila, "Estudiantes"));
        Assert.Equal("40 %", Celda(doc, "Estrategias", fila, "Avance"));
        Assert.All(doc.Tablas[0].Filas, f => Assert.Equal("Pacífico", f[0]));
        Assert.True(doc.GeneradoEn > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact] // CU-04 pasos 4-5 · RQ_09
    public async Task El_desempeno_consolida_indicadores_por_tutor()
    {
        await EstrategiaConActividad(await api.ClienteComo("tutor.pacifico"), "Desempeño Pacífico");
        var funcionario = await api.ClienteComo("funcionario.men");

        var doc = await (await funcionario.GetAsync("/api/reportes/desempeno?region=Pacífico")).Leer<ReporteDocumentoDto>();

        var fila = Fila(doc, "Desempeño por tutor", "Andrés Mosquera Rentería");
        Assert.True(int.Parse(Celda(doc, "Desempeño por tutor", fila, "Estrategias")) >= 1);
        Assert.True(int.Parse(Celda(doc, "Desempeño por tutor", fila, "Seguimientos")) >= 1);
        Assert.Contains(doc.Cifras, c => c.Etiqueta == "avance promedio" && c.Destacada);
    }

    [Fact] // CU-04 · RQ_09: actividades realizadas
    public async Task Actividades_lista_cada_acompanamiento_con_su_ultimo_seguimiento()
    {
        var estrategia = await EstrategiaConActividad(await api.ClienteComo("tutor.pacifico"), "Actividades Pacífico");
        var funcionario = await api.ClienteComo("funcionario.men");

        var doc = await (await funcionario.GetAsync("/api/reportes/actividades?mes=2026-11&entidadId=4")).Leer<ReporteDocumentoDto>();

        var fila = Fila(doc, "Acompañamientos", estrategia.Nombre);
        Assert.Equal("Individual", Celda(doc, "Acompañamientos", fila, "Tipo"));
        Assert.Contains("Revisión del planeador", Celda(doc, "Acompañamientos", fila, "Último seguimiento"));
    }

    [Fact] // CU-04 · RQ_10: reporte de ley de cobertura
    public async Task La_cobertura_calcula_estudiantes_vinculados_por_entidad()
    {
        await EstrategiaConActividad(await api.ClienteComo("tutor.pacifico"), "Cobertura Pacífico");
        var funcionario = await api.ClienteComo("funcionario.men");

        var doc = await (await funcionario.GetAsync("/api/reportes/ley-cobertura?periodo=2026-2")).Leer<ReporteDocumentoDto>();

        var fila = Fila(doc, "Cobertura por entidad", "IE Normal Superior de Quibdó");
        Assert.Equal("20", Celda(doc, "Cobertura por entidad", fila, "Estudiantes precargados"));
        Assert.True(int.Parse(Celda(doc, "Cobertura por entidad", fila, "Estudiantes vinculados")) >= 1);
        Assert.EndsWith("%", Celda(doc, "Cobertura por entidad", fila, "Cobertura"));
        Assert.Equal(6, doc.Tablas[0].Filas.Length);
    }

    [Fact] // CU-04 · RQ_10: reporte de ley 1581
    public async Task El_reporte_de_datos_personales_inventaria_titulares_y_accesos()
    {
        var funcionario = await api.ClienteComo("funcionario.men");

        var doc = await (await funcionario.GetAsync("/api/reportes/ley-datos-personales")).Leer<ReporteDocumentoDto>();

        var estudiantes = Fila(doc, "Datos personales tratados", "Estudiantes (menores de edad)");
        Assert.Equal("120", Celda(doc, "Datos personales tratados", estudiantes, "Titulares"));
        Assert.Contains(doc.Tablas.Single(t => t.Titulo == "Accesos y operaciones registradas").Filas, f => f[0] == "Ingresó al sistema");
        Assert.DoesNotContain(doc.Tablas.SelectMany(t => t.Filas).SelectMany(f => f), c => c.StartsWith("1100000"));
    }

    [Fact] // Filtro de mes en el consolidado
    public async Task El_filtro_de_mes_limita_los_acompanamientos()
    {
        var estrategia = await EstrategiaConActividad(await api.ClienteComo("tutor.pacifico"), "Filtro por mes");
        var admin = await api.ClienteComo("admin.mineducacion");

        var octubre = await (await admin.GetAsync("/api/reportes/consolidado?entidadId=4&mes=2026-10")).Leer<ReporteDocumentoDto>();

        var fila = Fila(octubre, "Estrategias", estrategia.Nombre);
        Assert.Equal("0", Celda(octubre, "Estrategias", fila, "Acompañamientos"));
    }

    [Fact] // CU-04 y CU-05 flujo alterno 4A
    public async Task Sin_informacion_el_reporte_lo_indica()
    {
        var ente = await api.ClienteComo("ente.control");

        var doc = await (await ente.GetAsync("/api/reportes/consolidado?region=Amazonía&periodo=2019-1")).Leer<ReporteDocumentoDto>();

        Assert.True(doc.SinDatos);
        Assert.Empty(doc.Tablas[0].Filas);
    }

    // ---------- Trazabilidad (CU-04 paso 7, CU-05 paso 6, RQ_14) ----------

    [Fact]
    public async Task Generar_y_descargar_quedan_en_la_trazabilidad()
    {
        var ente = await api.ClienteComo("ente.control");
        await ente.GetAsync("/api/reportes/consolidado?region=Caribe");

        var descarga = await ente.PostAsJsonAsync("/api/reportes/descargas",
            new { tipo = "consolidado", formato = "pdf", filtros = "Región: Caribe" });

        Assert.Equal(HttpStatusCode.NoContent, descarga.StatusCode);
        var admin = await api.ClienteComo("admin.mineducacion");
        var generados = await (await admin.GetAsync("/api/auditoria?usuario=ente.control&operacion=reporte.generar")).Leer<AuditoriaDto[]>();
        var descargados = await (await admin.GetAsync("/api/auditoria?usuario=ente.control&operacion=reporte.descargar")).Leer<AuditoriaDto[]>();
        Assert.Contains(generados, r => r.RecursoId == "consolidado" && r.Detalle == "Región: Caribe");
        Assert.Contains(descargados, r => r.RecursoId == "consolidado.pdf" && r.Detalle == "Región: Caribe");
    }

    [Fact]
    public async Task Descargas_no_validas_se_rechazan()
    {
        var ente = await api.ClienteComo("ente.control");

        var formato = await ente.PostAsJsonAsync("/api/reportes/descargas", new { tipo = "consolidado", formato = "exe", filtros = "" });
        var tipo = await ente.PostAsJsonAsync("/api/reportes/descargas", new { tipo = "ley-cobertura", formato = "csv", filtros = "" });

        Assert.Equal(HttpStatusCode.BadRequest, formato.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tipo.StatusCode);
    }

    [Fact]
    public async Task Los_filtros_ofrecen_regiones_entidades_tutores_y_periodos()
    {
        await EstrategiaConActividad(await api.ClienteComo("tutor.pacifico"), "Filtros disponibles");
        var funcionario = await api.ClienteComo("funcionario.men");

        var filtros = await (await funcionario.GetAsync("/api/reportes/filtros")).Leer<FiltrosReporteDto>();

        Assert.Contains("Pacífico", filtros.Regiones);
        Assert.Equal(6, filtros.Entidades.Length);
        Assert.Contains(filtros.Tutores, t => t.Nombre == "Andrés Mosquera Rentería");
        Assert.DoesNotContain(filtros.Tutores, t => t.Nombre == "Contraloría Delegada");
        Assert.Contains("2026-2", filtros.Periodos);
        Assert.Contains("2026-11", filtros.Meses);
    }
}
