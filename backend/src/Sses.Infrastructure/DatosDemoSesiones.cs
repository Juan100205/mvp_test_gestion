using Microsoft.EntityFrameworkCore;
using Sses.Domain;

namespace Sses.Infrastructure;

/// <summary>
/// Sesiones de pilotaje simuladas para la demostración (CU-07): dos terminadas con asistencia, uso, incidentes y encuesta,
/// una en curso para que el tutor pueda ingresar y una programada. Se cargan solo si aún no hay sesiones, así que también
/// funcionan en bases que ya tenían los datos demo de los Sprints 1 a 3. Nunca se cargan en las pruebas.
/// </summary>
public static class DatosDemoSesiones
{
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    public static async Task CargarAsync(SsesDbContext db, DateTimeOffset ahora, CancellationToken ct = default)
    {
        if (await db.Sesiones.AnyAsync(ct)) return;

        var usuarios = await db.Usuarios.ToDictionaryAsync(u => u.Username, ct);
        if (!usuarios.TryGetValue("lider.piloto", out var lider)) return;
        var caribe = usuarios["tutor.caribe"];
        var pacifico = usuarios["tutor.pacifico"];
        var hoy = new DateTimeOffset(ahora.ToOffset(Colombia).Date, Colombia).ToUniversalTime();
        ahora = ahora.ToUniversalTime();
        var auditoria = new List<RegistroAuditoria>();
        var metricas = new List<MetricaPeticion>();
        var aleatorio = new Random(2026);

        SesionPilotaje Sesion(string nombre, string region, DateTimeOffset inicio, int minutos, params Usuario[] tutores)
        {
            var programadaEn = inicio.AddDays(-5);
            var sesion = SesionPilotaje.Programar(nombre, region, inicio, minutos, tutores, lider.Id, programadaEn);
            db.Sesiones.Add(sesion);
            return sesion;
        }

        void Uso(Usuario tutor, DateTimeOffset desde, int peticiones)
        {
            string[] rutas = ["/api/estrategias", "/api/estrategias/{id:int}", "/api/estrategias/{id:int}/acompanamientos",
                "/api/acompanamientos/{id:guid}/seguimientos/{semana:int}", "/api/mis-sesiones"];
            for (var i = 0; i < peticiones; i++)
            {
                var ruta = rutas[aleatorio.Next(rutas.Length)];
                metricas.Add(new MetricaPeticion(tutor.Id, ruta.Contains("seguimientos") ? "PUT" : "GET", ruta, 200,
                    aleatorio.Next(80, 900), desde.AddMinutes(3 + i * 4)));
            }
        }

        // Terminada: Caribe, hace una semana.
        var s1 = Sesion("Sesión 1 · Caribe", "Caribe", hoy.AddDays(-7).AddHours(9), 120, caribe);
        // Terminada: Pacífico, hace tres días.
        var s2 = Sesion("Sesión 1 · Pacífico", "Pacífico", hoy.AddDays(-3).AddHours(14), 120, pacifico);
        // En curso: el tutor del Caribe puede ingresar durante la demostración.
        var s3 = Sesion("Sesión 2 · Caribe", "Caribe", ahora.AddMinutes(-20), 240, caribe);
        // Programada: Pacífico, la próxima semana.
        var s4 = Sesion("Sesión 2 · Pacífico", "Pacífico", hoy.AddDays(7).AddHours(9), 120, pacifico);
        await db.SaveChangesAsync(ct);

        foreach (var s in new[] { s1, s2, s3, s4 })
            auditoria.Add(new RegistroAuditoria(lider.Id, lider.Username, "sesion.programar", "sesion", s.Id.ToString(),
                $"{s.Nombre} · {s.Region}", s.CreadaEn));

        void Terminada(SesionPilotaje s, Usuario tutor, int llegada, int?[] respuestas, string comentario)
        {
            var ingreso = s.Inicio.AddMinutes(llegada);
            s.RegistrarAsistencia(tutor.Id, ingreso);
            auditoria.Add(new RegistroAuditoria(tutor.Id, tutor.Username, "sesion.asistencia", "sesion", s.Id.ToString(), s.Nombre, ingreso));
            Uso(tutor, ingreso, 22);
            var respondida = s.Fin.AddMinutes(-10);
            s.ResponderEncuesta(tutor.Id, respuestas, comentario, respondida);
            auditoria.Add(new RegistroAuditoria(tutor.Id, tutor.Username, "sesion.encuesta", "sesion", s.Id.ToString(), s.Nombre, respondida));
        }

        Terminada(s1, caribe, 4, [5, 4, 4, 5, 5], "Registrar el seguimiento desde el celular fue más rápido que en el Excel.");
        s1.RegistrarIncidente("La sede no tenía internet los primeros 20 minutos; se trabajó sin señal y sincronizó al volver.", s1.Inicio.AddMinutes(15));
        auditoria.Add(new RegistroAuditoria(caribe.Id, caribe.Username, "auth.fallido", "usuario", caribe.Id.ToString(), null, s1.Inicio.AddMinutes(2)));

        Terminada(s2, pacifico, 9, [4, 4, 3, 5, 4], "Me gustaría ver el historial de todos los docentes en una sola pantalla.");
        s2.RegistrarIncidente("Un tutor no recordaba su contraseña; se restableció con la Oficina de Tecnología.", s2.Inicio.AddMinutes(5));

        db.Auditoria.AddRange(auditoria.OrderBy(a => a.Fecha));
        db.Metricas.AddRange(metricas);
        await db.SaveChangesAsync(ct);
    }
}
