using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendamentoClinica.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTipoConsultaEmConsulta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "consultas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Retorno");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo",
                table: "consultas");
        }
    }
}
