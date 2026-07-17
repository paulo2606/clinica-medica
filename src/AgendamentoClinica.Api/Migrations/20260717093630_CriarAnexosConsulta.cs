using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendamentoClinica.Api.Migrations
{
    /// <inheritdoc />
    public partial class CriarAnexosConsulta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anexos_consulta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consulta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_original = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    extensao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    enviado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anexos_consulta", x => x.id);
                    table.ForeignKey(
                        name: "FK_anexos_consulta_consultas_consulta_id",
                        column: x => x.consulta_id,
                        principalTable: "consultas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anexos_consulta_consulta_id",
                table: "anexos_consulta",
                column: "consulta_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anexos_consulta");
        }
    }
}
