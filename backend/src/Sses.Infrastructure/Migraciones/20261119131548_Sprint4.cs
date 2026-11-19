using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sses.Infrastructure.Migraciones
{
    /// <inheritdoc />
    public partial class Sprint4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "metricas_peticiones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Metodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Ruta = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Milisegundos = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metricas_peticiones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_pilotaje",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Region = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LiderId = table.Column<int>(type: "integer", nullable: false),
                    Convocados = table.Column<List<int>>(type: "integer[]", nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones_pilotaje", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sesiones_pilotaje_usuarios_LiderId",
                        column: x => x.LiderId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sesion_asistencias",
                columns: table => new
                {
                    SesionId = table.Column<int>(type: "integer", nullable: false),
                    TutorId = table.Column<int>(type: "integer", nullable: false),
                    IngresoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_asistencias", x => new { x.SesionId, x.TutorId });
                    table.ForeignKey(
                        name: "FK_sesion_asistencias_sesiones_pilotaje_SesionId",
                        column: x => x.SesionId,
                        principalTable: "sesiones_pilotaje",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sesion_asistencias_usuarios_TutorId",
                        column: x => x.TutorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sesion_encuestas",
                columns: table => new
                {
                    SesionId = table.Column<int>(type: "integer", nullable: false),
                    TutorId = table.Column<int>(type: "integer", nullable: false),
                    Respuestas = table.Column<List<int>>(type: "integer[]", nullable: false),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RespondidaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_encuestas", x => new { x.SesionId, x.TutorId });
                    table.ForeignKey(
                        name: "FK_sesion_encuestas_sesiones_pilotaje_SesionId",
                        column: x => x.SesionId,
                        principalTable: "sesiones_pilotaje",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sesion_encuestas_usuarios_TutorId",
                        column: x => x.TutorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sesion_incidentes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SesionId = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_incidentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sesion_incidentes_sesiones_pilotaje_SesionId",
                        column: x => x.SesionId,
                        principalTable: "sesiones_pilotaje",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_metricas_peticiones_UsuarioId_Fecha",
                table: "metricas_peticiones",
                columns: new[] { "UsuarioId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_sesion_asistencias_TutorId",
                table: "sesion_asistencias",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_encuestas_TutorId",
                table: "sesion_encuestas",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_incidentes_SesionId",
                table: "sesion_incidentes",
                column: "SesionId");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_pilotaje_Convocados",
                table: "sesiones_pilotaje",
                column: "Convocados")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_pilotaje_LiderId",
                table: "sesiones_pilotaje",
                column: "LiderId");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_pilotaje_Region_Inicio",
                table: "sesiones_pilotaje",
                columns: new[] { "Region", "Inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metricas_peticiones");

            migrationBuilder.DropTable(
                name: "sesion_asistencias");

            migrationBuilder.DropTable(
                name: "sesion_encuestas");

            migrationBuilder.DropTable(
                name: "sesion_incidentes");

            migrationBuilder.DropTable(
                name: "sesiones_pilotaje");
        }
    }
}
