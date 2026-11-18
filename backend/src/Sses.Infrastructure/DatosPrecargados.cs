using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sses.Domain;

namespace Sses.Infrastructure;

/// <summary>
/// Simula la información que el Ministerio entrega precargada (entidades, docentes, estudiantes y usuarios).
/// Solo para ambientes de desarrollo, pruebas y demostración.
/// </summary>
public static class DatosPrecargados
{
    public const string ClaveDemo = "Piloto2026*";

    private static readonly (string Nombre, string Region, string Municipio)[] Entidades =
    [
        ("IE Técnica Simón Bolívar", "Caribe", "Barranquilla"),
        ("IE San José de Turbaco", "Caribe", "Turbaco"),
        ("IE Policarpa Salavarrieta", "Andina", "Tunja"),
        ("IE Normal Superior de Quibdó", "Pacífico", "Quibdó"),
        ("IE Agropecuaria La Esperanza", "Orinoquía", "Yopal"),
        ("IE Indígena Yuruparí", "Amazonía", "Mitú"),
    ];

    private static readonly string[] Nombres =
    [
        "Valentina", "Santiago", "Mariana", "Juan Pablo", "Isabella", "Samuel", "Sara", "Matías", "Luciana", "Sebastián",
        "Daniela", "Emmanuel", "Gabriela", "Jerónimo", "Salomé", "Tomás", "Antonella", "Martín", "Manuela", "Nicolás",
    ];

    private static readonly string[] Apellidos =
    [
        "Rodríguez", "Martínez", "Mosquera", "Hernández", "Palacios", "Gómez", "Rentería", "Díaz", "Ospina", "Cuesta",
        "Restrepo", "Torres", "Vargas", "Córdoba", "Castillo", "Pérez", "Murillo", "Suárez", "Ramírez", "Valencia",
    ];

    private static readonly string[] Docentes =
    [
        "Marta Lucía Ruiz", "Óscar Iván Peña", "Claudia Patricia Mena", "Hernán Darío Rojas", "Yolanda Cáceres",
        "Wilson Arboleda", "Luz Marina Quintero", "Fredy Alexander Lozano", "Gloria Inés Bermúdez", "Jhon Jairo Salas",
        "Nubia Esperanza Ortiz", "Carlos Arturo Benítez", "Diana Marcela Paz", "Edwin Mauricio Lara", "Amparo Velásquez",
        "Rubén Darío Chaverra", "Esperanza Gil", "Álvaro Hinestroza", "Patricia Moreno", "Jairo Andrés Cuéllar",
        "Sandra Milena Asprilla", "Fabián Ricardo Mejía", "Ruth Mery Cardona", "Ignacio Tunubalá",
    ];

    public static async Task InicializarAsync(SsesDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        if (await db.Usuarios.AnyAsync(ct)) return;

        for (var i = 0; i < Entidades.Length; i++)
        {
            var (nombre, region, municipio) = Entidades[i];
            db.Entidades.Add(new Entidad(i + 1, nombre, region, municipio));
        }

        // 4 docentes por entidad: la entidad 1 tiene los docentes 1 a 4, la 2 los 5 a 8, etc.
        for (var i = 0; i < Docentes.Length; i++)
            db.Docentes.Add(new Docente(i + 1, $"{52_000_000 + i * 7919}", Docentes[i], i / 4 + 1));

        // Estudiantes menores de edad con tarjeta de identidad 11000000NN.
        for (var i = 1; i <= 120; i++)
        {
            var nombre = $"{Nombres[(i * 7) % Nombres.Length]} {Apellidos[(i * 3) % Apellidos.Length]} {Apellidos[(i * 11) % Apellidos.Length]}";
            db.Estudiantes.Add(new Estudiante(i, "TI", $"11{i:D8}", nombre, (i - 1) % Entidades.Length + 1));
        }

        var hasher = new PasswordHasher<Usuario>();
        var usuarios = new (string Username, string Nombre, Rol Rol, string Region)[]
        {
            ("tutor.caribe", "Laura Gómez Palacios", Rol.Tutor, "Caribe"),
            ("tutor.pacifico", "Andrés Mosquera Rentería", Rol.Tutor, "Pacífico"),
            ("tutor.bloqueo", "Cuenta de prueba de bloqueo", Rol.Tutor, "Andina"),
            ("funcionario.men", "Catalina Ríos", Rol.Funcionario, "Nacional"),
            ("ente.control", "Contraloría Delegada", Rol.EnteControl, "Nacional"),
            ("lider.piloto", "Felipe Cárdenas", Rol.LiderPilotaje, "Caribe"),
            ("admin.mineducacion", "Oficina de Tecnología", Rol.Administrador, "Nacional"),
        };
        for (var i = 0; i < usuarios.Length; i++)
        {
            var u = usuarios[i];
            var borrador = new Usuario(i + 1, u.Username, "", u.Nombre, u.Rol, u.Region);
            db.Usuarios.Add(new Usuario(i + 1, u.Username, hasher.HashPassword(borrador, ClaveDemo), u.Nombre, u.Rol, u.Region));
        }

        await db.SaveChangesAsync(ct);
    }
}
