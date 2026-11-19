using Sses.Domain;

namespace Sses.Domain.Tests;

/// <summary>CU-07 (HU-11, HU-12) · RQ_15, RQ_16.</summary>
public class SesionPilotajeTests
{
    private static readonly DateTimeOffset Inicio = Fabrica.Ahora.AddDays(1);

    private static Usuario Tutor(int id, string region = "Caribe") => new(id, $"tutor{id}", "", $"Tutor {id}", Rol.Tutor, region);

    private static SesionPilotaje Sesion(params Usuario[] tutores) =>
        SesionPilotaje.Programar("Sesión 1 Caribe", "Caribe", Inicio, 120, tutores.Length > 0 ? tutores : [Tutor(1), Tutor(2)],
            liderId: 6, Fabrica.Ahora);

    private static int?[] Completa => [5, 4, 4, 5, 5];

    [Fact] // CU-07 paso 2
    public void Programar_guarda_region_horario_y_convocados()
    {
        var sesion = Sesion();

        Assert.Equal("Caribe", sesion.Region);
        Assert.Equal(Inicio.AddMinutes(120), sesion.Fin);
        Assert.Equal([1, 2], sesion.Convocados);
        Assert.Equal(EstadoSesion.Programada, sesion.Estado(Fabrica.Ahora));
    }

    [Fact] // PostgreSQL solo guarda timestamptz en UTC
    public void El_horario_se_guarda_en_utc()
    {
        var sesion = Sesion();

        Assert.Equal(TimeSpan.Zero, sesion.Inicio.Offset);
        Assert.Equal(Inicio, sesion.Inicio);
    }

    [Fact]
    public void Programar_exige_nombre_region_y_al_menos_un_tutor()
    {
        var error = Assert.Throws<ReglaNegocioException>(() =>
            SesionPilotaje.Programar(" ", "", Inicio, 120, [], 6, Fabrica.Ahora));

        Assert.Equal(Errores.DatosIncompletos, error.Codigo);
        Assert.Equal(["nombre", "region", "tutores"], error.Campos);
    }

    [Fact] // Las sesiones son por región
    public void No_convoca_tutores_de_otra_region()
    {
        var error = Assert.Throws<ReglaNegocioException>(() => Sesion(Tutor(1), Tutor(3, "Andina")));

        Assert.Equal(Errores.TutorDeOtraRegion, error.Codigo);
        Assert.Equal(["tutores"], error.Campos);
    }

    [Fact]
    public void No_convoca_usuarios_que_no_son_tutores()
    {
        var admin = new Usuario(7, "admin", "", "Admin", Rol.Administrador, "Caribe");

        Assert.Equal(Errores.TutorDeOtraRegion, Assert.Throws<ReglaNegocioException>(() => Sesion(admin)).Codigo);
    }

    [Theory]
    [InlineData(-60, 120)] // en el pasado
    [InlineData(60, 10)] // duración muy corta
    [InlineData(60, 600)] // duración muy larga
    public void Rechaza_fecha_pasada_o_duracion_fuera_de_rango(int minutosDesdeAhora, int duracion)
    {
        var error = Assert.Throws<ReglaNegocioException>(() =>
            SesionPilotaje.Programar("Sesión", "Caribe", Fabrica.Ahora.AddMinutes(minutosDesdeAhora), duracion, [Tutor(1)], 6, Fabrica.Ahora));

        Assert.Equal(Errores.HorarioNoValido, error.Codigo);
    }

    [Fact] // CU-07 paso 3
    public void El_tutor_convocado_registra_asistencia_al_ingresar()
    {
        var sesion = Sesion();
        var llegada = Inicio.AddMinutes(5);

        var asistencia = sesion.RegistrarAsistencia(1, llegada);

        Assert.Equal(llegada, asistencia.IngresoEn);
        Assert.Equal(EstadoSesion.EnCurso, sesion.Estado(llegada));
        Assert.True(sesion.Asistio(1));
    }

    [Fact]
    public void Ingresar_dos_veces_no_duplica_la_asistencia()
    {
        var sesion = Sesion();
        var primera = sesion.RegistrarAsistencia(1, Inicio);

        var segunda = sesion.RegistrarAsistencia(1, Inicio.AddMinutes(20));

        Assert.Same(primera, segunda);
        Assert.Single(sesion.Asistencias);
    }

    [Fact] // Se puede ingresar desde 30 minutos antes
    public void Permite_ingresar_desde_media_hora_antes()
    {
        var sesion = Sesion();

        sesion.RegistrarAsistencia(1, Inicio.AddMinutes(-30));

        Assert.True(sesion.Asistio(1));
    }

    [Theory]
    [InlineData(-31)]
    [InlineData(121)]
    public void Fuera_del_horario_no_registra_asistencia(int minutos)
    {
        var error = Assert.Throws<ReglaNegocioException>(() => Sesion().RegistrarAsistencia(1, Inicio.AddMinutes(minutos)));

        Assert.Equal(Errores.SesionFueraDeHorario, error.Codigo);
    }

    [Fact] // Flujo alterno 3A
    public void Un_tutor_no_convocado_no_puede_ingresar()
    {
        var error = Assert.Throws<ReglaNegocioException>(() => Sesion().RegistrarAsistencia(99, Inicio));

        Assert.Equal(Errores.NoConvocado, error.Codigo);
    }

    [Fact] // CU-07 paso 5
    public void El_tutor_que_asistio_responde_la_encuesta()
    {
        var sesion = Sesion();
        sesion.RegistrarAsistencia(1, Inicio);

        var encuesta = sesion.ResponderEncuesta(1, Completa, "  Muy útil sin señal  ", Inicio.AddMinutes(110));

        Assert.Equal([5, 4, 4, 5, 5], encuesta.Respuestas);
        Assert.Equal("Muy útil sin señal", encuesta.Comentario);
        Assert.True(sesion.RespondioEncuesta(1));
    }

    [Fact] // Flujo alterno 5A
    public void La_encuesta_incompleta_indica_las_preguntas_pendientes()
    {
        var sesion = Sesion();
        sesion.RegistrarAsistencia(1, Inicio);

        var error = Assert.Throws<ReglaNegocioException>(() => sesion.ResponderEncuesta(1, [5, null, 4, null, 5], null, Inicio));

        Assert.Equal(Errores.DatosIncompletos, error.Codigo);
        Assert.Equal(["p2", "p4"], error.Campos);
        Assert.Contains(SesionPilotaje.Preguntas[1].Texto, error.Message);
        Assert.False(sesion.RespondioEncuesta(1));
    }

    [Fact]
    public void Las_respuestas_van_de_1_a_5()
    {
        var sesion = Sesion();
        sesion.RegistrarAsistencia(1, Inicio);

        var error = Assert.Throws<ReglaNegocioException>(() => sesion.ResponderEncuesta(1, [5, 4, 9, 5, 0], null, Inicio));

        Assert.Equal(Errores.RespuestaNoValida, error.Codigo);
        Assert.Equal(["p3", "p5"], error.Campos);
    }

    [Fact]
    public void Solo_responde_quien_asistio_y_una_sola_vez()
    {
        var sesion = Sesion();

        Assert.Equal(Errores.SinAsistencia,
            Assert.Throws<ReglaNegocioException>(() => sesion.ResponderEncuesta(1, Completa, null, Inicio)).Codigo);

        sesion.RegistrarAsistencia(1, Inicio);
        sesion.ResponderEncuesta(1, Completa, null, Inicio);
        Assert.Equal(Errores.EncuestaYaRespondida,
            Assert.Throws<ReglaNegocioException>(() => sesion.ResponderEncuesta(1, Completa, null, Inicio)).Codigo);
    }

    [Fact] // CU-07 paso 6: incidentes manuales del líder
    public void El_lider_registra_incidentes()
    {
        var sesion = Sesion();

        var incidente = sesion.RegistrarIncidente("  Se cayó el wifi de la sede  ", Inicio.AddMinutes(15));

        Assert.Equal("Se cayó el wifi de la sede", incidente.Descripcion);
        Assert.Single(sesion.Incidentes);
        Assert.Equal(Errores.DatosIncompletos,
            Assert.Throws<ReglaNegocioException>(() => sesion.RegistrarIncidente(" ", Inicio)).Codigo);
    }

    [Fact]
    public void Termina_al_pasar_la_hora_de_fin()
    {
        Assert.Equal(EstadoSesion.Terminada, Sesion().Estado(Inicio.AddMinutes(121)));
    }
}
