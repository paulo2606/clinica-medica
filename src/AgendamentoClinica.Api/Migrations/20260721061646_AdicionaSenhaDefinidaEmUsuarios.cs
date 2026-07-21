using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendamentoClinica.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSenhaDefinidaEmUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true porque contas já existentes foram criadas com senha
            // real e usável desde o início (fluxo antigo, sem convite por e-mail);
            // só contas novas (convite/médico) nascem com false.
            migrationBuilder.AddColumn<bool>(
                name: "senha_definida",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "senha_definida",
                table: "usuarios");
        }
    }
}
