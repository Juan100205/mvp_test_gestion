using Sses.Domain;

namespace Sses.Domain.Tests;

/// <summary>CU-03 (RQ_06, RQ_07).</summary>
public class AcompanamientoTests
{
    private static readonly DateOnly Noviembre = new(2026, 11, 1);

    private static Acompanamiento Individual(Estrategia estrategia, int docenteId = 1) =>
        Acompanamiento.Registrar(Guid.NewGuid(), estrategia, TipoAcompanamiento.Individual, Noviembre,
            "Fortalecer la planeación de clase", "Entregar planeación ajustada", [docenteId], tutorId: 10, Fabrica.Ahora);

    [Fact]
    public void Registrar_individual_con_un_docente_de_la_estrategia()
    {
        var acompanamiento = Individual(Fabrica.Estrategia());

        Assert.Equal(TipoAcompanamiento.Individual, acompanamiento.Tipo);
        Assert.Equal([1], acompanamiento.DocenteIds);
        Assert.Equal("1", acompanamiento.ClaveGrupo);
    }

    [Fact]
    public void El_periodo_se_normaliza_al_primer_dia_del_mes()
    {
        var a = Acompanamiento.Registrar(Guid.NewGuid(), Fabrica.Estrategia(), TipoAcompanamiento.Individual,
            new DateOnly(2026, 11, 23), "Objetivo", "Compromisos", [1], 10, Fabrica.Ahora);

        Assert.Equal(Noviembre, a.Periodo);
    }

    [Fact]
    public void Registrar_grupal_ordena_los_docentes_en_la_clave_del_grupo()
    {
        var a = Acompanamiento.Registrar(Guid.NewGuid(), Fabrica.Estrategia(), TipoAcompanamiento.Grupal, Noviembre,
            "Objetivo", "Compromisos", [3, 1], 10, Fabrica.Ahora);

        Assert.Equal("1-3", a.ClaveGrupo);
    }

    [Theory]
    [InlineData(TipoAcompanamiento.Individual, new[] { 1, 2 })]
    [InlineData(TipoAcompanamiento.Individual, new int[0])]
    [InlineData(TipoAcompanamiento.Grupal, new[] { 1 })]
    public void El_numero_de_docentes_debe_corresponder_al_tipo(TipoAcompanamiento tipo, int[] docentes)
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            Acompanamiento.Registrar(Guid.NewGuid(), Fabrica.Estrategia(), tipo, Noviembre, "Objetivo", "Compromisos", docentes, 10, Fabrica.Ahora));

        Assert.Equal(Errores.DocentesNoCorrespondenAlTipo, ex.Codigo);
    }

    [Fact] // RQ_06: solo docentes asociados a la estrategia
    public void Registrar_con_docente_no_asociado_a_la_estrategia_es_rechazado()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => Individual(Fabrica.Estrategia(), docenteId: 99));

        Assert.Equal(Errores.DocenteNoAsociado, ex.Codigo);
    }

    [Fact]
    public void Registrar_sin_objetivo_informa_datos_incompletos()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            Acompanamiento.Registrar(Guid.NewGuid(), Fabrica.Estrategia(), TipoAcompanamiento.Individual, Noviembre, " ", "c", [1], 10, Fabrica.Ahora));

        Assert.Equal(Errores.DatosIncompletos, ex.Codigo);
        Assert.Contains("objetivo", ex.Campos);
    }

    [Fact] // CU-03 paso 5
    public void Registrar_seguimiento_semanal_lo_liga_al_acompanamiento()
    {
        var acompanamiento = Individual(Fabrica.Estrategia());

        var seguimiento = acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), 1, "Observación de clase", 25, "Buen avance", Fabrica.Ahora);

        Assert.Single(acompanamiento.Seguimientos);
        Assert.Equal(acompanamiento.Id, seguimiento.AcompanamientoId);
    }

    [Fact] // Flujo alterno 5A: semana ya registrada se actualiza, no se duplica
    public void Registrar_seguimiento_de_semana_existente_lo_actualiza()
    {
        var acompanamiento = Individual(Fabrica.Estrategia());
        var primero = acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), 2, "Visita", 30, "", Fabrica.Ahora);

        var segundo = acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), 2, "Visita y retroalimentación", 60, "Mejoró", Fabrica.Ahora.AddDays(1));

        Assert.Single(acompanamiento.Seguimientos);
        Assert.Same(primero, segundo);
        Assert.Equal(60, segundo.Avance);
        Assert.Equal("Visita y retroalimentación", segundo.Actividades);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(6, 50)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public void Seguimiento_fuera_de_rango_es_rechazado(int semana, int avance)
    {
        var acompanamiento = Individual(Fabrica.Estrategia());

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), semana, "Actividad", avance, "", Fabrica.Ahora));

        Assert.Equal(Errores.SeguimientoInvalido, ex.Codigo);
    }

    [Fact]
    public void El_avance_del_mes_es_el_ultimo_avance_registrado()
    {
        var acompanamiento = Individual(Fabrica.Estrategia());
        acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), 1, "a", 20, "", Fabrica.Ahora);
        acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), 3, "b", 70, "", Fabrica.Ahora);
        acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), 2, "c", 45, "", Fabrica.Ahora);

        Assert.Equal(70, acompanamiento.AvanceActual);
    }
}
