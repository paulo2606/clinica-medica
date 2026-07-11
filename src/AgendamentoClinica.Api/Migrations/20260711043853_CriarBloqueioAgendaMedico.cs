using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendamentoClinica.Api.Migrations
{
    /// <inheritdoc />
    public partial class CriarBloqueioAgendaMedico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bloqueios_agenda_medico",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_hora_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo_recorrencia = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    recorrencia_ate = table.Column<DateOnly>(type: "date", nullable: true),
                    regra_recorrencia = table.Column<string>(type: "text", nullable: true),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bloqueios_agenda_medico", x => x.id);
                    table.ForeignKey(
                        name: "FK_bloqueios_agenda_medico_medicos_medico_id",
                        column: x => x.medico_id,
                        principalTable: "medicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bloqueios_agenda_medico_medico_id",
                table: "bloqueios_agenda_medico",
                column: "medico_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bloqueios_agenda_medico");
        }
    }
}
