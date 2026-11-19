namespace Sses.Domain;

/// <summary>Violación de una regla del negocio. El código es estable y lo consume el front.</summary>
public sealed class ReglaNegocioException(string codigo, string mensaje, IReadOnlyList<string>? campos = null)
    : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
    public IReadOnlyList<string> Campos { get; } = campos ?? [];
}

public static class Errores
{
    public const string DatosIncompletos = "datos_incompletos";
    public const string EntidadNoValida = "entidad_no_valida";
    public const string DocenteDeOtraEntidad = "docente_de_otra_entidad";
    public const string EstrategiaBloqueada = "estrategia_bloqueada";
    public const string EstudianteYaVinculado = "estudiante_ya_vinculado";
    public const string DocenteNoAsociado = "docente_no_asociado";
    public const string DocentesNoCorrespondenAlTipo = "docentes_no_corresponden_al_tipo";
    public const string SeguimientoInvalido = "seguimiento_invalido";
    public const string TutorDeOtraRegion = "tutor_de_otra_region";
    public const string HorarioNoValido = "horario_no_valido";
    public const string NoConvocado = "no_convocado";
    public const string SesionFueraDeHorario = "sesion_fuera_de_horario";
    public const string SinAsistencia = "sin_asistencia";
    public const string EncuestaYaRespondida = "encuesta_ya_respondida";
    public const string RespuestaNoValida = "respuesta_no_valida";
}

public enum Rol
{
    Tutor,
    Funcionario,
    EnteControl,
    LiderPilotaje,
    Administrador,
}

internal static class Guardas
{
    public static void Requeridos(params (string campo, string? valor)[] campos)
    {
        var faltantes = campos.Where(c => string.IsNullOrWhiteSpace(c.valor)).Select(c => c.campo).ToList();
        if (faltantes.Count > 0)
            throw new ReglaNegocioException(Errores.DatosIncompletos,
                $"Completa estos datos antes de continuar: {string.Join(", ", faltantes)}.", faltantes);
    }
}
