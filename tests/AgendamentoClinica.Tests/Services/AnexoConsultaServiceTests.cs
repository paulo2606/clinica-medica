using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class AnexoConsultaServiceTests
{
    private class AmbienteFake : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Testes";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "agendamento-testes-anexos", Guid.NewGuid().ToString());
        public string EnvironmentName { get; set; } = "Testes";
    }

    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    private static async Task<(Guid MedicoId, Guid ConsultaId)> CriarMedicoEConsultaAsync(AgendamentoDbContext db)
    {
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        db.Especialidades.Add(especialidade);
        var usuarioMedico = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Bruno Medico",
            Email = $"{Guid.NewGuid()}@clinica.com",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            SenhaHash = "hash",
            Papel = PapelUsuario.Medico,
            Ativo = true
        };
        db.Usuarios.Add(usuarioMedico);
        var medico = new Medico { Id = Guid.NewGuid(), UsuarioId = usuarioMedico.Id, EspecialidadeId = especialidade.Id, Crm = $"CRM{Random.Shared.Next(1000000, 9999999)}" };
        db.Medicos.Add(medico);
        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Paciente Teste",
            Cpf = $"{Random.Shared.NextInt64(10000000000, 99999999999)}",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            DataNascimento = new DateOnly(1990, 1, 1)
        };
        db.Pacientes.Add(paciente);
        var consulta = new Consulta
        {
            Id = Guid.NewGuid(),
            MedicoId = medico.Id,
            PacienteId = paciente.Id,
            DataHora = new DateTime(2026, 7, 20, 11, 0, 0, DateTimeKind.Utc),
            DuracaoMinutos = 20,
            Status = StatusConsulta.Agendada,
            CriadoPorUsuarioId = usuarioMedico.Id
        };
        db.Consultas.Add(consulta);
        await db.SaveChangesAsync();
        return (medico.Id, consulta.Id);
    }

    [Fact]
    public async Task AdicionarAsync_ComConsultaExistente_DeveGravarArquivoEInserirLinha()
    {
        var db = CriarDbContext();
        var (medicoId, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());

        var (resultado, anexoId) = await servico.AdicionarAsync(
            consultaId, medicoId, Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", "laudo.pdf");

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.NotNull(anexoId);
        var anexo = await db.AnexosConsulta.FindAsync(anexoId);
        Assert.NotNull(anexo);
        Assert.Equal("laudo.pdf", anexo!.NomeOriginal);
    }

    [Fact]
    public async Task AdicionarAsync_ComConsultaDeOutroMedico_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var (_, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());

        var (resultado, anexoId) = await servico.AdicionarAsync(
            consultaId, Guid.NewGuid(), Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", "laudo.pdf");

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(anexoId);
    }

    [Fact]
    public async Task AdicionarAsync_ComQuatroAnexos_DeveRetornarLimiteExcedidoNoQuarto()
    {
        var db = CriarDbContext();
        var (medicoId, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());
        for (var i = 0; i < 3; i++)
        {
            await servico.AdicionarAsync(consultaId, medicoId, Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", $"arquivo{i}.pdf");
        }

        var (resultado, anexoId) = await servico.AdicionarAsync(
            consultaId, medicoId, Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", "arquivo4.pdf");

        Assert.Equal(ResultadoOperacao.LimiteExcedido, resultado);
        Assert.Null(anexoId);
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarAnexosOrdenadosPorCriadoEm()
    {
        var db = CriarDbContext();
        var (medicoId, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());
        await servico.AdicionarAsync(consultaId, medicoId, Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", "primeiro.pdf");
        await servico.AdicionarAsync(consultaId, medicoId, Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", "segundo.pdf");

        var (resultado, anexos) = await servico.ListarAsync(consultaId, medicoId);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.Equal(2, anexos.Count);
        Assert.Equal("primeiro.pdf", anexos[0].NomeOriginal);
    }

    [Fact]
    public async Task ObterConteudoAsync_DeveRetornarOsBytesGravados()
    {
        var db = CriarDbContext();
        var (medicoId, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());
        byte[] conteudoOriginal = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x01, 0x02];
        var (_, anexoId) = await servico.AdicionarAsync(consultaId, medicoId, Guid.NewGuid(), conteudoOriginal, "pdf", "laudo.pdf");

        var (resultado, conteudo, anexo) = await servico.ObterConteudoAsync(consultaId, anexoId!.Value, medicoId);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.Equal(conteudoOriginal, conteudo);
        Assert.Equal("laudo.pdf", anexo!.NomeOriginal);
    }

    [Fact]
    public async Task ObterConteudoAsync_ComRestricaoDeOutroMedico_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var (medicoId, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());
        var (_, anexoId) = await servico.AdicionarAsync(consultaId, medicoId, Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", "laudo.pdf");

        var (resultado, conteudo, anexo) = await servico.ObterConteudoAsync(consultaId, anexoId!.Value, Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(conteudo);
        Assert.Null(anexo);
    }

    [Fact]
    public async Task RemoverAsync_DeveApagarArquivoELinha()
    {
        var db = CriarDbContext();
        var (medicoId, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());
        var (_, anexoId) = await servico.AdicionarAsync(consultaId, medicoId, Guid.NewGuid(), [0x25, 0x50, 0x44, 0x46, 0x2D], "pdf", "laudo.pdf");

        var resultado = await servico.RemoverAsync(consultaId, anexoId!.Value, medicoId);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.Null(await db.AnexosConsulta.FindAsync(anexoId));
    }

    [Fact]
    public async Task RemoverAsync_ComAnexoInexistente_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var (medicoId, consultaId) = await CriarMedicoEConsultaAsync(db);
        var servico = new AnexoConsultaService(db, new AmbienteFake());

        var resultado = await servico.RemoverAsync(consultaId, Guid.NewGuid(), medicoId);

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }
}
