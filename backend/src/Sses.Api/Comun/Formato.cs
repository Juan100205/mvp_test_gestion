using System.Globalization;

namespace Sses.Api.Comun;

/// <summary>Textos y formatos de los documentos que genera el API (reportes y consolidado de sesiones).</summary>
public static class Formato
{
    // Sin depender de ICU: la imagen de producción puede correr en modo de globalización invariante.
    private static readonly string[] Meses =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];
    private static readonly TimeSpan HoraColombia = TimeSpan.FromHours(-5);

    /// <summary>Mismas etiquetas que el front (web/src/lib/formato.ts).</summary>
    public static readonly IReadOnlyDictionary<string, string> Operaciones = new Dictionary<string, string>
    {
        ["auth.ingreso"] = "Ingresó al sistema",
        ["auth.fallido"] = "Intento de ingreso fallido",
        ["auth.bloqueo"] = "Cuenta bloqueada por intentos",
        ["estrategia.crear"] = "Creó una estrategia",
        ["estrategia.editar"] = "Editó una estrategia",
        ["estrategia.vincular_estudiante"] = "Vinculó un estudiante",
        ["acompanamiento.registrar"] = "Registró un acompañamiento",
        ["seguimiento.registrar"] = "Registró un seguimiento semanal",
        ["seguimiento.actualizar"] = "Actualizó un seguimiento semanal",
        ["reporte.generar"] = "Generó un reporte",
        ["reporte.descargar"] = "Descargó un reporte",
        ["sesion.programar"] = "Programó una sesión de pilotaje",
        ["sesion.asistencia"] = "Ingresó a una sesión de pilotaje",
        ["sesion.encuesta"] = "Respondió la encuesta de percepción",
        ["sesion.incidente"] = "Registró un incidente de sesión",
        ["sesion.exportar"] = "Exportó el registro de una sesión",
    };

    public static string Operacion(string codigo) => Operaciones.GetValueOrDefault(codigo, codigo);

    public static string Entero(int valor) => valor.ToString(CultureInfo.InvariantCulture);
    public static string Porcentaje(int valor) => $"{valor} %";
    public static string Decimal1(double valor) => valor.ToString("0.0", CultureInfo.InvariantCulture);

    public static int PorcentajeDe(int parte, int total) =>
        total == 0 ? 0 : (int)Math.Round(parte * 100.0 / total, MidpointRounding.AwayFromZero);

    public static int Promedio(IEnumerable<int> valores)
    {
        var lista = valores.ToList();
        return lista.Count == 0 ? 0 : (int)Math.Round(lista.Average(), MidpointRounding.AwayFromZero);
    }

    public static string Hora(DateTimeOffset fecha)
    {
        var local = fecha.ToOffset(HoraColombia);
        var hora = local.Hour % 12 == 0 ? 12 : local.Hour % 12;
        return $"{hora}:{local.Minute:D2} {(local.Hour < 12 ? "a. m." : "p. m.")}";
    }

    public static string Fecha(DateTimeOffset fecha)
    {
        var local = fecha.ToOffset(HoraColombia);
        return $"{local.Day} de {Meses[local.Month - 1]} de {local.Year}";
    }

    public static string FechaHora(DateTimeOffset fecha) => $"{Fecha(fecha)}, {Hora(fecha)}";

    public static string Mes(DateOnly periodo) => $"{Meses[periodo.Month - 1]} de {periodo.Year}";

    public static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : string.Concat(texto.AsSpan(0, maximo - 1), "…");
}
