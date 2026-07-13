using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendamentoClinica.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarLembreteEnviadoEmConsulta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "lembrete_enviado_em",
                table: "consultas",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lembrete_enviado_em",
                table: "consultas");
        }
    }
}
