using Sses.Domain;

namespace Sses.Domain.Tests;

/// <summary>Datos de prueba reutilizables (equivalen a la información precargada del piloto).</summary>
internal static class Fabrica
{
    public static readonly DateTimeOffset Ahora = new(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(-5));

    public static Entidad Entidad(int id = 1) => new(id, $"IE Entidad {id}", "Caribe", "Barranquilla");

    public static Docente Docente(int id, int entidadId = 1) => new(id, $"10{id:D6}", $"Docente {id}", entidadId);

    public static Estudiante Estudiante(int id, int entidadId = 1) =>
        new(id, "TI", $"11{id:D8}", $"Estudiante {id}", entidadId);

    public static Estrategia Estrategia(params Docente[] docentes)
    {
        var entidad = Entidad();
        var lista = docentes.Length > 0 ? docentes : [Docente(1), Docente(2), Docente(3)];
        return Domain.Estrategia.Crear(entidad, tutorId: 10, "Lectura en voz alta", "Club de lectura semanal", "2026-2", lista, Ahora);
    }
}
