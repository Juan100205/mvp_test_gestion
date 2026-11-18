using Sses.Domain;

namespace Sses.Domain.Tests;

/// <summary>CU-06 flujo alterno 1A (RQ_13): bloqueo temporal al quinto intento fallido.</summary>
public class UsuarioTests
{
    private static Usuario Tutor() => new(1, "tutor.caribe", "hash", "Laura Gómez", Rol.Tutor, "Caribe");

    [Fact]
    public void Cuatro_intentos_fallidos_no_bloquean_la_cuenta()
    {
        var usuario = Tutor();
        for (var i = 0; i < 4; i++) usuario.RegistrarIntentoFallido(Fabrica.Ahora);

        Assert.False(usuario.EstaBloqueado(Fabrica.Ahora));
        Assert.Equal(1, usuario.IntentosRestantes);
    }

    [Fact]
    public void El_quinto_intento_fallido_bloquea_temporalmente()
    {
        var usuario = Tutor();
        for (var i = 0; i < 5; i++) usuario.RegistrarIntentoFallido(Fabrica.Ahora);

        Assert.True(usuario.EstaBloqueado(Fabrica.Ahora));
        Assert.True(usuario.EstaBloqueado(Fabrica.Ahora.Add(Usuario.DuracionBloqueo).AddSeconds(-1)));
    }

    [Fact]
    public void El_bloqueo_expira_y_reinicia_los_intentos()
    {
        var usuario = Tutor();
        for (var i = 0; i < 5; i++) usuario.RegistrarIntentoFallido(Fabrica.Ahora);
        var despues = Fabrica.Ahora.Add(Usuario.DuracionBloqueo);

        Assert.False(usuario.EstaBloqueado(despues));
        usuario.RegistrarIntentoFallido(despues);
        Assert.False(usuario.EstaBloqueado(despues));
    }

    [Fact]
    public void Un_ingreso_exitoso_reinicia_el_contador()
    {
        var usuario = Tutor();
        for (var i = 0; i < 3; i++) usuario.RegistrarIntentoFallido(Fabrica.Ahora);

        usuario.RegistrarIngresoExitoso(Fabrica.Ahora);

        Assert.Equal(Usuario.MaxIntentos, usuario.IntentosRestantes);
        Assert.Equal(Fabrica.Ahora, usuario.UltimoIngreso);
    }
}
