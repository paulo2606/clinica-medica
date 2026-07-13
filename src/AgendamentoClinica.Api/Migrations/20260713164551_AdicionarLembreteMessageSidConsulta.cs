using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendamentoClinica.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarLembreteMessageSidConsulta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lembrete_message_sid",
                table: "consultas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lembrete_message_sid",
                table: "consultas");
        }
    }
}
