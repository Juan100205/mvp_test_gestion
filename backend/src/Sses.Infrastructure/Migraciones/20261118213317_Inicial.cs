using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sses.Infrastructure.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Usuario = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Operacion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Recurso = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RecursoId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "entidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entidades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Region = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UltimoIngreso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "docentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntidadId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_docentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_docentes_entidades_EntidadId",
                        column: x => x.EntidadId,
                        principalTable: "entidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estudiantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    TipoDocumento = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntidadId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estudiantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_estudiantes_entidades_EntidadId",
                        column: x => x.EntidadId,
                        principalTable: "entidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estrategias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntidadId = table.Column<int>(type: "integer", nullable: false),
                    TutorId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Periodo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estrategias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_estrategias_entidades_EntidadId",
                        column: x => x.EntidadId,
                        principalTable: "entidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_estrategias_usuarios_TutorId",
                        column: x => x.TutorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "acompanamientos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstrategiaId = table.Column<int>(type: "integer", nullable: false),
                    TutorId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    Objetivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Compromisos = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DocenteIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    ClaveGrupo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acompanamientos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_acompanamientos_estrategias_EstrategiaId",
                        column: x => x.EstrategiaId,
                        principalTable: "estrategias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estrategia_docentes",
                columns: table => new
                {
                    EstrategiaId = table.Column<int>(type: "integer", nullable: false),
                    DocenteId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estrategia_docentes", x => new { x.EstrategiaId, x.DocenteId });
                    table.ForeignKey(
                        name: "FK_estrategia_docentes_docentes_DocenteId",
                        column: x => x.DocenteId,
                        principalTable: "docentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_estrategia_docentes_estrategias_EstrategiaId",
                        column: x => x.EstrategiaId,
                        principalTable: "estrategias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "estrategia_estudiantes",
                columns: table => new
                {
                    EstrategiaId = table.Column<int>(type: "integer", nullable: false),
                    EstudianteId = table.Column<int>(type: "integer", nullable: false),
                    VinculadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estrategia_estudiantes", x => new { x.EstrategiaId, x.EstudianteId });
                    table.ForeignKey(
                        name: "FK_estrategia_estudiantes_estrategias_EstrategiaId",
                        column: x => x.EstrategiaId,
                        principalTable: "estrategias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_estrategia_estudiantes_estudiantes_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "estudiantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimientos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcompanamientoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Semana = table.Column<int>(type: "integer", nullable: false),
                    Actividades = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Avance = table.Column<int>(type: "integer", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seguimientos", x => x.Id);
                    table.CheckConstraint("ck_seguimiento_avance", "\"Avance\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_seguimiento_semana", "\"Semana\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_seguimientos_acompanamientos_AcompanamientoId",
                        column: x => x.AcompanamientoId,
                        principalTable: "acompanamientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_acompanamientos_DocenteIds",
                table: "acompanamientos",
                column: "DocenteIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_acompanamientos_EstrategiaId_Periodo_Tipo_ClaveGrupo",
                table: "acompanamientos",
                columns: new[] { "EstrategiaId", "Periodo", "Tipo", "ClaveGrupo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_Operacion_Fecha",
                table: "auditoria",
                columns: new[] { "Operacion", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_Usuario_Fecha",
                table: "auditoria",
                columns: new[] { "Usuario", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_docentes_Documento",
                table: "docentes",
                column: "Documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_docentes_EntidadId",
                table: "docentes",
                column: "EntidadId");

            migrationBuilder.CreateIndex(
                name: "IX_entidades_Region",
                table: "entidades",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_estrategia_docentes_DocenteId",
                table: "estrategia_docentes",
                column: "DocenteId");

            migrationBuilder.CreateIndex(
                name: "IX_estrategia_estudiantes_EstudianteId",
                table: "estrategia_estudiantes",
                column: "EstudianteId");

            migrationBuilder.CreateIndex(
                name: "IX_estrategias_EntidadId",
                table: "estrategias",
                column: "EntidadId");

            migrationBuilder.CreateIndex(
                name: "IX_estrategias_TutorId",
                table: "estrategias",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_estudiantes_EntidadId",
                table: "estudiantes",
                column: "EntidadId");

            migrationBuilder.CreateIndex(
                name: "IX_estudiantes_NumeroDocumento",
                table: "estudiantes",
                column: "NumeroDocumento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seguimientos_AcompanamientoId_Semana",
                table: "seguimientos",
                columns: new[] { "AcompanamientoId", "Semana" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Username",
                table: "usuarios",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "estrategia_docentes");

            migrationBuilder.DropTable(
                name: "estrategia_estudiantes");

            migrationBuilder.DropTable(
                name: "seguimientos");

            migrationBuilder.DropTable(
                name: "docentes");

            migrationBuilder.DropTable(
                name: "estudiantes");

            migrationBuilder.DropTable(
                name: "acompanamientos");

            migrationBuilder.DropTable(
                name: "estrategias");

            migrationBuilder.DropTable(
                name: "entidades");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
