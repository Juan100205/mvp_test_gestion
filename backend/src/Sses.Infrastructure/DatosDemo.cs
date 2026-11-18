using Microsoft.EntityFrameworkCore;
using Sses.Domain;

namespace Sses.Infrastructure;

/// <summary>
/// Historia de uso simulada para la demostración: estrategias, estudiantes, acompañamientos de los últimos
/// tres meses y su rastro de auditoría. Las fechas se calculan desde hoy para que el mes en curso siempre
/// tenga datos. Solo se carga si no hay estrategias y si la configuración lo pide (nunca en las pruebas).
/// </summary>
public static class DatosDemo
{
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    private static readonly string[] Actividades =
    [
        "Revisión del planeador de la semana y ajuste de los objetivos de la sesión",
        "Observación de clase de 45 minutos con la rúbrica de acompañamiento",
        "Retroalimentación de la observación y acuerdos para la siguiente sesión",
        "Taller sobre preguntas abiertas para la lectura en voz alta",
        "Revisión de las bitácoras de los estudiantes",
        "Planeación conjunta de la actividad de cierre del mes",
        "Visita a la sede rural y revisión de materiales",
        "Socialización de avances con el coordinador académico",
    ];

    private static readonly string[] Observaciones =
    [
        "",
        "La docente propuso incluir a las familias en la actividad de cierre.",
        "Faltó conectividad en la sede; se registró al volver al casco urbano.",
        "Se reprogramó un día por jornada de vacunación en la institución.",
        "",
    ];

    private record Plan(string Username, int Entidad, string Nombre, string Descripcion, int[] Docentes, int[] Estudiantes,
        (TipoAcompanamiento Tipo, int[] Docentes, string Objetivo, int[] MesesAtras)[] Acompanamientos);

    private static readonly Plan[] Planes =
    [
        new("tutor.caribe", 1, "Club de lectura en voz alta",
            "Lectura compartida dos veces por semana con estudiantes de 6.º y 7.º; cada grupo lleva una bitácora de lecturas.",
            [1, 2, 3], [1, 7, 13, 19, 25],
            [
                (TipoAcompanamiento.Individual, [1], "Mejorar la formulación de preguntas durante la lectura", [2, 1, 0]),
                (TipoAcompanamiento.Grupal, [2, 3], "Unificar la rúbrica de comprensión lectora entre los dos grados", [1, 0]),
            ]),
        new("tutor.caribe", 2, "Semillero de robótica con material reciclado",
            "Construcción de prototipos con cartón, motores de juguetes viejos y placas donadas.",
            [5, 6], [],
            [(TipoAcompanamiento.Individual, [5], "Organizar las sesiones prácticas por retos semanales", [0])]),
        new("tutor.caribe", 1, "Huerta escolar y matemáticas",
            "Medición de parcelas, registro de crecimiento y gráficas con los datos de la huerta.",
            [4], [], []),
        new("tutor.pacifico", 4, "Semillero de ciencia en el río Atrato",
            "Experimentos con agua del río, registro en bitácora y presentación de resultados a la comunidad.",
            [13, 14, 15], [4, 10, 16, 22],
            [
                (TipoAcompanamiento.Individual, [15], "Fortalecer la indagación científica en clase", [1, 0]),
                (TipoAcompanamiento.Grupal, [13, 14], "Diseñar en conjunto la feria de ciencia del semestre", [0]),
            ]),
        new("tutor.pacifico", 4, "Chirimía y memoria del Pacífico",
            "Práctica musical con instrumentos tradicionales y entrevistas a mayores de la comunidad.",
            [16], [28, 34],
            [(TipoAcompanamiento.Individual, [16], "Planear el repertorio y las entrevistas del semestre", [2, 1])]),
        new("tutor.bloqueo", 3, "Bandas de paz",
            "Ensayos de banda marcial en jornada contraria como alternativa de uso del tiempo libre.",
            [9, 10], [3, 9],
            [(TipoAcompanamiento.Individual, [9], "Mejorar la asistencia a los ensayos", [1])]),
    ];

    public static async Task CargarAsync(SsesDbContext db, DateTimeOffset ahora, CancellationToken ct = default)
    {
        if (await db.Estrategias.AnyAsync(ct)) return;

        var usuarios = await db.Usuarios.ToDictionaryAsync(u => u.Username, ct);
        var entidades = await db.Entidades.ToDictionaryAsync(e => e.Id, ct);
        var docentes = await db.Docentes.ToDictionaryAsync(d => d.Id, ct);
        var estudiantes = await db.Estudiantes.ToDictionaryAsync(s => s.Id, ct);
        var hoyLocal = ahora.ToOffset(Colombia);
        var inicioMes = new DateTimeOffset(hoyLocal.Year, hoyLocal.Month, 1, 0, 0, 0, Colombia).ToUniversalTime(); // Npgsql solo acepta UTC
        var periodo = $"{hoyLocal.Year}-{(hoyLocal.Month <= 6 ? 1 : 2)}";
        var auditoria = new List<RegistroAuditoria>();
        var creadas = new List<(Plan Plan, Estrategia Estrategia)>();

        void Auditar(Usuario u, DateTimeOffset fecha, string operacion, string recurso, object? id, string? detalle) =>
            auditoria.Add(new RegistroAuditoria(u.Id, u.Username, operacion, recurso, id?.ToString(), detalle, fecha));

        // 1. Estrategias (el Id lo asigna la base), creadas al inicio de hace dos meses.
        for (var i = 0; i < Planes.Length; i++)
        {
            var plan = Planes[i];
            var creadaEn = inicioMes.AddMonths(-2).AddDays(1 + i).AddHours(9);
            var estrategia = Estrategia.Crear(entidades[plan.Entidad], usuarios[plan.Username].Id, plan.Nombre, plan.Descripcion,
                periodo, plan.Docentes.Select(d => docentes[d]), creadaEn);
            db.Estrategias.Add(estrategia);
            creadas.Add((plan, estrategia));
        }
        await db.SaveChangesAsync(ct);

        foreach (var (plan, estrategia) in creadas)
        {
            var tutor = usuarios[plan.Username];
            var creadaEn = estrategia.CreadaEn;
            Auditar(tutor, creadaEn.AddMinutes(-3), "auth.ingreso", "usuario", tutor.Id, null);
            Auditar(tutor, creadaEn, "estrategia.crear", "estrategia", estrategia.Id, estrategia.Nombre);

            // 2. Estudiantes vinculados unos días después: la estrategia queda solo para consulta (RQ_03).
            for (var j = 0; j < plan.Estudiantes.Length; j++)
            {
                var estudiante = estudiantes[plan.Estudiantes[j]];
                var fecha = creadaEn.AddDays(3).AddMinutes(4 * j);
                estrategia.VincularEstudiante(estudiante, fecha);
                Auditar(tutor, fecha, "estrategia.vincular_estudiante", "estrategia", estrategia.Id, $"estudiante {estudiante.NumeroDocumento}");
            }

            // 3. Acompañamientos mensuales con seguimiento semanal hasta la fecha de hoy.
            foreach (var (tipo, docenteIds, objetivo, mesesAtras) in plan.Acompanamientos)
            {
                foreach (var atras in mesesAtras)
                {
                    var mes = inicioMes.AddMonths(-atras);
                    var registradoEn = mes.AddDays(1).AddHours(10);
                    if (registradoEn > ahora) continue;

                    var acompanamiento = Acompanamiento.Registrar(Guid.NewGuid(), estrategia, tipo, DateOnly.FromDateTime(mes.ToOffset(Colombia).Date),
                        objetivo, "Enviar el planeador ajustado antes de la siguiente visita.", docenteIds, tutor.Id, registradoEn);
                    Auditar(tutor, registradoEn, "acompanamiento.registrar", "acompanamiento", acompanamiento.Id,
                        $"{tipo} {mes.ToOffset(Colombia):yyyy-MM}");

                    var semilla = estrategia.Id * 13 + atras * 5 + docenteIds[0];
                    var avance = 0;
                    for (var semana = 1; semana <= Acompanamiento.SemanasPorMes; semana++)
                    {
                        var fecha = mes.AddDays((semana - 1) * 7 + 1).AddHours(15);
                        if (fecha > ahora || fecha.ToOffset(Colombia).Month != mes.ToOffset(Colombia).Month) break;
                        avance = Math.Min(100, avance + 10 + (semilla * 7 + semana * 3) % 4 * 5);
                        acompanamiento.RegistrarSeguimiento(Guid.NewGuid(), semana, Actividades[(semilla + semana) % Actividades.Length],
                            avance, Observaciones[(semilla + semana) % Observaciones.Length], fecha);
                        Auditar(tutor, fecha, "seguimiento.registrar", "acompanamiento", acompanamiento.Id,
                            $"semana {semana}, avance {avance} %");
                    }

                    db.Acompanamientos.Add(acompanamiento);
                }
            }
        }

        // 4. Rastro del resto de perfiles: ingresos, un intento fallido y el bloqueo de la cuenta de prueba.
        var ayer = new DateTimeOffset(hoyLocal.Date.AddDays(-1), Colombia).ToUniversalTime();
        foreach (var (username, hora) in new[] { ("admin.mineducacion", 8), ("funcionario.men", 9), ("ente.control", 11), ("lider.piloto", 14) })
        {
            var u = usuarios[username];
            Auditar(u, ayer.AddHours(hora), "auth.ingreso", "usuario", u.Id, null);
        }
        var pacifico = usuarios["tutor.pacifico"];
        auditoria.Add(new RegistroAuditoria(pacifico.Id, pacifico.Username, "auth.fallido", "usuario", pacifico.Id.ToString(), null, ayer.AddHours(7).AddMinutes(52)));
        var bloqueo = usuarios["tutor.bloqueo"];
        for (var k = 0; k < 4; k++)
            Auditar(bloqueo, ayer.AddHours(16).AddMinutes(k), "auth.fallido", "usuario", bloqueo.Id, null);
        Auditar(bloqueo, ayer.AddHours(16).AddMinutes(4), "auth.bloqueo", "usuario", bloqueo.Id, null);
        auditoria.Add(new RegistroAuditoria(null, "profe.ruiz", "auth.fallido", "usuario", null, "usuario inexistente", ayer.AddHours(18)));

        db.Auditoria.AddRange(auditoria.OrderBy(a => a.Fecha));
        await db.SaveChangesAsync(ct);
    }
}
