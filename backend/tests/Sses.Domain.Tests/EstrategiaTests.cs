using Sses.Domain;

namespace Sses.Domain.Tests;

/// <summary>CU-01 (RQ_01, RQ_02, RQ_03) y CU-02 (RQ_05).</summary>
public class EstrategiaTests
{
    [Fact]
    public void Crear_con_entidad_definida_queda_editable_y_asociada()
    {
        var estrategia = Fabrica.Estrategia();

        Assert.Equal(1, estrategia.EntidadId);
        Assert.True(estrategia.EsEditable);
        Assert.Equal(EstadoEstrategia.Editable, estrategia.Estado);
        Assert.Equal(3, estrategia.Docentes.Count);
    }

    [Theory] // Flujo alterno 4A: datos incompletos
    [InlineData("", "2026-2")]
    [InlineData("   ", "2026-2")]
    [InlineData("Lectura", "")]
    public void Crear_con_datos_incompletos_informa_que_corregir(string nombre, string periodo)
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            Estrategia.Crear(Fabrica.Entidad(), 10, nombre, "desc", periodo, [Fabrica.Docente(1)], Fabrica.Ahora));

        Assert.Equal(Errores.DatosIncompletos, ex.Codigo);
        Assert.NotEmpty(ex.Campos);
    }

    [Fact] // Flujo alterno 4A: entidad no válida (docente de otra entidad)
    public void Crear_con_docentes_de_otra_entidad_es_rechazado()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            Estrategia.Crear(Fabrica.Entidad(1), 10, "Lectura", "desc", "2026-2", [Fabrica.Docente(1, entidadId: 2)], Fabrica.Ahora));

        Assert.Equal(Errores.DocenteDeOtraEntidad, ex.Codigo);
    }

    [Fact]
    public void Crear_sin_docentes_es_rechazado()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            Estrategia.Crear(Fabrica.Entidad(), 10, "Lectura", "desc", "2026-2", [], Fabrica.Ahora));

        Assert.Equal(Errores.DatosIncompletos, ex.Codigo);
    }

    [Fact] // Flujo principal pasos 6-7
    public void Editar_sin_estudiantes_actualiza_los_datos()
    {
        var estrategia = Fabrica.Estrategia();

        estrategia.Editar("Lectura crítica", "Nueva descripción", "2026-2", [Fabrica.Docente(1)], Fabrica.Ahora);

        Assert.Equal("Lectura crítica", estrategia.Nombre);
        Assert.Single(estrategia.Docentes);
    }

    [Fact] // Flujo alterno 6A
    public void Editar_con_estudiantes_vinculados_se_bloquea()
    {
        var estrategia = Fabrica.Estrategia();
        estrategia.VincularEstudiante(Fabrica.Estudiante(1), Fabrica.Ahora);

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            estrategia.Editar("Otro", "desc", "2026-2", [Fabrica.Docente(1)], Fabrica.Ahora));

        Assert.Equal(Errores.EstrategiaBloqueada, ex.Codigo);
        Assert.False(estrategia.EsEditable);
        Assert.Equal(EstadoEstrategia.SoloConsulta, estrategia.Estado);
    }

    [Fact] // CU-02 paso 4
    public void Vincular_estudiante_lo_agrega_al_listado()
    {
        var estrategia = Fabrica.Estrategia();

        estrategia.VincularEstudiante(Fabrica.Estudiante(1), Fabrica.Ahora);
        estrategia.VincularEstudiante(Fabrica.Estudiante(2), Fabrica.Ahora);

        Assert.Equal(2, estrategia.Estudiantes.Count);
    }

    [Fact] // RQ_05: sin duplicados
    public void Vincular_dos_veces_al_mismo_estudiante_es_rechazado()
    {
        var estrategia = Fabrica.Estrategia();
        estrategia.VincularEstudiante(Fabrica.Estudiante(1), Fabrica.Ahora);

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            estrategia.VincularEstudiante(Fabrica.Estudiante(1), Fabrica.Ahora));

        Assert.Equal(Errores.EstudianteYaVinculado, ex.Codigo);
        Assert.Single(estrategia.Estudiantes);
    }
}
