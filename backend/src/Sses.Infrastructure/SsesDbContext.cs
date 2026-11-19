using Microsoft.EntityFrameworkCore;
using Sses.Domain;

namespace Sses.Infrastructure;

public class SsesDbContext(DbContextOptions<SsesDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Entidad> Entidades => Set<Entidad>();
    public DbSet<Docente> Docentes => Set<Docente>();
    public DbSet<Estudiante> Estudiantes => Set<Estudiante>();
    public DbSet<Estrategia> Estrategias => Set<Estrategia>();
    public DbSet<Acompanamiento> Acompanamientos => Set<Acompanamiento>();
    public DbSet<Seguimiento> Seguimientos => Set<Seguimiento>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
    public DbSet<SesionPilotaje> Sesiones => Set<SesionPilotaje>();
    public DbSet<MetricaPeticion> Metricas => Set<MetricaPeticion>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Catálogos precargados: conservan el Id del sistema de origen.
        b.Entity<Entidad>(e =>
        {
            e.ToTable("entidades");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(200);
            e.Property(x => x.Region).HasMaxLength(60);
            e.Property(x => x.Municipio).HasMaxLength(100);
            e.HasIndex(x => x.Region);
        });

        b.Entity<Docente>(e =>
        {
            e.ToTable("docentes");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Documento).HasMaxLength(20);
            e.Property(x => x.Nombre).HasMaxLength(200);
            e.HasIndex(x => x.Documento).IsUnique();
            e.HasIndex(x => x.EntidadId);
            e.HasOne<Entidad>().WithMany().HasForeignKey(x => x.EntidadId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Estudiante>(e =>
        {
            e.ToTable("estudiantes");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TipoDocumento).HasMaxLength(4);
            e.Property(x => x.NumeroDocumento).HasMaxLength(20);
            e.Property(x => x.Nombre).HasMaxLength(200);
            // RQ_18: búsqueda por documento indexada sobre millones de registros.
            e.HasIndex(x => x.NumeroDocumento).IsUnique();
            e.HasOne<Entidad>().WithMany().HasForeignKey(x => x.EntidadId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Username).HasMaxLength(80);
            e.Property(x => x.Nombre).HasMaxLength(200);
            e.Property(x => x.Region).HasMaxLength(60);
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Username).IsUnique();
        });

        b.Entity<Estrategia>(e =>
        {
            e.ToTable("estrategias");
            e.Property(x => x.Nombre).HasMaxLength(150);
            e.Property(x => x.Descripcion).HasMaxLength(1000);
            e.Property(x => x.Periodo).HasMaxLength(20);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.TutorId);
            e.HasOne<Entidad>().WithMany().HasForeignKey(x => x.EntidadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.EsEditable);
            e.Ignore(x => x.Estado);

            e.HasMany(x => x.Docentes).WithOne().HasForeignKey(x => x.EstrategiaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Docentes).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.Estudiantes).WithOne().HasForeignKey(x => x.EstrategiaId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Estudiantes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<EstrategiaDocente>(e =>
        {
            e.ToTable("estrategia_docentes");
            e.HasKey(x => new { x.EstrategiaId, x.DocenteId });
            e.HasOne<Docente>().WithMany().HasForeignKey(x => x.DocenteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EstrategiaEstudiante>(e =>
        {
            e.ToTable("estrategia_estudiantes");
            // RQ_05: la llave primaria impide vincular dos veces al mismo estudiante.
            e.HasKey(x => new { x.EstrategiaId, x.EstudianteId });
            e.HasOne<Estudiante>().WithMany().HasForeignKey(x => x.EstudianteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Acompanamiento>(e =>
        {
            e.ToTable("acompanamientos");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(12);
            e.Property(x => x.Objetivo).HasMaxLength(1000);
            e.Property(x => x.Compromisos).HasMaxLength(1000);
            e.Property(x => x.ClaveGrupo).HasMaxLength(400);
            e.Ignore(x => x.AvanceActual);
            e.HasIndex(x => x.DocenteIds).HasMethod("gin"); // historial por docente (RQ_08, RQ_18)
            // Flujo alterno 4A: un acompañamiento por docente o grupo y por mes.
            e.HasIndex(x => new { x.EstrategiaId, x.Periodo, x.Tipo, x.ClaveGrupo }).IsUnique();
            e.HasOne<Estrategia>().WithMany().HasForeignKey(x => x.EstrategiaId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Seguimientos).WithOne().HasForeignKey(x => x.AcompanamientoId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Seguimientos).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<Seguimiento>(e =>
        {
            e.ToTable("seguimientos");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Actividades).HasMaxLength(2000);
            e.Property(x => x.Observaciones).HasMaxLength(2000);
            // Flujo alterno 5A: una fila por semana; se actualiza en lugar de duplicarse.
            e.HasIndex(x => new { x.AcompanamientoId, x.Semana }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_seguimiento_semana", "\"Semana\" BETWEEN 1 AND 5"));
            e.ToTable(t => t.HasCheckConstraint("ck_seguimiento_avance", "\"Avance\" BETWEEN 0 AND 100"));
        });

        // CU-07: sesiones de pilotaje (RQ_15, RQ_16).
        b.Entity<SesionPilotaje>(e =>
        {
            e.ToTable("sesiones_pilotaje");
            e.Property(x => x.Nombre).HasMaxLength(150);
            e.Property(x => x.Region).HasMaxLength(60);
            e.HasIndex(x => new { x.Region, x.Inicio });
            e.HasIndex(x => x.Convocados).HasMethod("gin"); // sesiones de un tutor
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.LiderId).OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Asistencias).WithOne().HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Asistencias).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.Incidentes).WithOne().HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Incidentes).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.Encuestas).WithOne().HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Encuestas).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<AsistenciaSesion>(e =>
        {
            e.ToTable("sesion_asistencias");
            // Una asistencia por tutor y sesión: ingresar dos veces no la duplica.
            e.HasKey(x => new { x.SesionId, x.TutorId });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<IncidenteSesion>(e =>
        {
            e.ToTable("sesion_incidentes");
            e.Property(x => x.Descripcion).HasMaxLength(500);
        });

        b.Entity<EncuestaPercepcion>(e =>
        {
            e.ToTable("sesion_encuestas");
            // Una encuesta por tutor y sesión.
            e.HasKey(x => new { x.SesionId, x.TutorId });
            e.Property(x => x.Comentario).HasMaxLength(1000);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<MetricaPeticion>(e =>
        {
            e.ToTable("metricas_peticiones");
            e.Property(x => x.Metodo).HasMaxLength(10);
            e.Property(x => x.Ruta).HasMaxLength(200);
            e.HasIndex(x => new { x.UsuarioId, x.Fecha });
        });

        b.Entity<RegistroAuditoria>(e =>
        {
            e.ToTable("auditoria");
            e.Property(x => x.Usuario).HasMaxLength(80);
            e.Property(x => x.Operacion).HasMaxLength(60);
            e.Property(x => x.Recurso).HasMaxLength(60);
            e.Property(x => x.RecursoId).HasMaxLength(60);
            e.Property(x => x.Detalle).HasMaxLength(500);
            e.HasIndex(x => new { x.Usuario, x.Fecha });
            e.HasIndex(x => new { x.Operacion, x.Fecha });
        });
    }
}
