using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendamentoClinica.Api.Migrations
{
    /// <inheritdoc />
    public partial class CriarEntidadesPrincipais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "especialidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_especialidades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pacientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pacientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tokens_renovacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens_renovacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_tokens_renovacao_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medicos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    especialidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    duracao_consulta_padrao_minutos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicos", x => x.id);
                    table.ForeignKey(
                        name: "FK_medicos_especialidades_especialidade_id",
                        column: x => x.especialidade_id,
                        principalTable: "especialidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medicos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consultas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duracao_minutos = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    criado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultas", x => x.id);
                    table.ForeignKey(
                        name: "FK_consultas_medicos_medico_id",
                        column: x => x.medico_id,
                        principalTable: "medicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consultas_pacientes_paciente_id",
                        column: x => x.paciente_id,
                        principalTable: "pacientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consultas_usuarios_criado_por_usuario_id",
                        column: x => x.criado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "horarios_trabalho_medico",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_semana = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_fim = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horarios_trabalho_medico", x => x.id);
                    table.ForeignKey(
                        name: "FK_horarios_trabalho_medico_medicos_medico_id",
                        column: x => x.medico_id,
                        principalTable: "medicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consultas_criado_por_usuario_id",
                table: "consultas",
                column: "criado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_consultas_medico_id",
                table: "consultas",
                column: "medico_id");

            migrationBuilder.CreateIndex(
                name: "IX_consultas_paciente_id",
                table: "consultas",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_especialidades_nome",
                table: "especialidades",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_horarios_trabalho_medico_medico_id",
                table: "horarios_trabalho_medico",
                column: "medico_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicos_crm",
                table: "medicos",
                column: "crm",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medicos_especialidade_id",
                table: "medicos",
                column: "especialidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicos_usuario_id",
                table: "medicos",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pacientes_cpf",
                table: "pacientes",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tokens_renovacao_token_hash",
                table: "tokens_renovacao",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tokens_renovacao_usuario_id",
                table: "tokens_renovacao",
                column: "usuario_id");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION periodo_consulta(data_hora timestamptz, duracao_minutos integer)
                RETURNS tstzrange
                LANGUAGE sql
                IMMUTABLE
                AS $$
                    SELECT tstzrange(data_hora, data_hora + (duracao_minutos * interval '1 minute'));
                $$;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE consultas
                ADD CONSTRAINT ck_consultas_sem_conflito_horario
                EXCLUDE USING gist (
                    medico_id WITH =,
                    periodo_consulta(data_hora, duracao_minutos) WITH &&
                )
                WHERE (status <> 'Cancelada');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE consultas DROP CONSTRAINT IF EXISTS ck_consultas_sem_conflito_horario;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS periodo_consulta(timestamptz, integer);");

            migrationBuilder.DropTable(
                name: "consultas");

            migrationBuilder.DropTable(
                name: "horarios_trabalho_medico");

            migrationBuilder.DropTable(
                name: "tokens_renovacao");

            migrationBuilder.DropTable(
                name: "pacientes");

            migrationBuilder.DropTable(
                name: "medicos");

            migrationBuilder.DropTable(
                name: "especialidades");
        }
    }
}
