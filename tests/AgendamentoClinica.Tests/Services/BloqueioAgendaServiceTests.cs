using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class BloqueioAgendaServiceTests
{
    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    private static async Task<Guid> CriarMedicoAsync(AgendamentoDbContext db)
    {
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        db.Especialidades.Add(especialidade);
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Bruno Medico",
            Email = $"{Guid.NewGuid()}@clinica.com",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            SenhaHash = "hash",
            Papel = PapelUsuario.Medico,
            Ativo = true
        };
        db.Usuarios.Add(usuario);
        var medico = new Medico
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            EspecialidadeId = especialidade.Id,
            Crm = $"CRM{Random.Shared.Next(1000000, 9999999)}",
            Ativo = true
        };
        db.Medicos.Add(medico);
        await db.SaveChangesAsync();
        return medico.Id;
    }

    [Fact]
    public async Task CriarAsync_ComDadosValidos_DeveCriarEGuardarRegraRecorrencia()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);

        var (resultado, id) = await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Semanal, null, "Compromisso pessoal");

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var bloqueio = (await servico.ListarPorMedicoAsync(medicoId)).Single();
        Assert.Equal(id, bloqueio.Id);
        Assert.NotNull(bloqueio.RegraRecorrencia);
    }

    [Fact]
    public async Task CriarAsync_SemRecorrencia_NaoDeveGuardarRegra()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);

        await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, null);

        var bloqueio = (await servico.ListarPorMedicoAsync(medicoId)).Single();
        Assert.Null(bloqueio.RegraRecorrencia);
    }

    [Fact]
    public async Task CriarAsync_ComMedicoInexistente_DeveRetornarNaoEncontrado()
    {
        var servico = new BloqueioAgendaService(CriarDbContext());

        var (resultado, id) = await servico.CriarAsync(
            Guid.NewGuid(), new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, null);

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task AtualizarAsync_SemRestricao_DeveAtualizarQualquerBloqueio()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);
        var (_, id) = await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, null);

        var resultado = await servico.AtualizarAsync(
            id!.Value, medicoIdRestricao: null,
            new DateTime(2026, 7, 14, 8, 0, 0), new DateTime(2026, 7, 14, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, "Atualizado");

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
    }

    [Fact]
    public async Task AtualizarAsync_ComRestricaoDeOutroMedico_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var outroMedicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);
        var (_, id) = await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, null);

        var resultado = await servico.AtualizarAsync(
            id!.Value, medicoIdRestricao: outroMedicoId,
            new DateTime(2026, 7, 14, 8, 0, 0), new DateTime(2026, 7, 14, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, "Tentativa indevida");

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task RemoverAsync_ComRestricaoDoProprioMedico_DeveRemover()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);
        var (_, id) = await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, null);

        var resultado = await servico.RemoverAsync(id!.Value, medicoIdRestricao: medicoId);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.Empty(await servico.ListarPorMedicoAsync(medicoId));
    }

    [Fact]
    public async Task RemoverAsync_ComRestricaoDeOutroMedico_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var outroMedicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);
        var (_, id) = await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, null);

        var resultado = await servico.RemoverAsync(id!.Value, medicoIdRestricao: outroMedicoId);

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Single(await servico.ListarPorMedicoAsync(medicoId));
    }

    [Fact]
    public async Task EstaBloqueadoAsync_ComBloqueioSobreposto_DeveRetornarTrue()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);
        await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, "Férias");

        var bloqueado = await servico.EstaBloqueadoAsync(
            medicoId, new DateTime(2026, 7, 13, 9, 0, 0), new DateTime(2026, 7, 13, 9, 20, 0));

        Assert.True(bloqueado);
    }

    [Fact]
    public async Task EstaBloqueadoAsync_SemSobreposicao_DeveRetornarFalse()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new BloqueioAgendaService(db);
        await servico.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0), new DateTime(2026, 7, 13, 12, 0, 0),
            TipoRecorrenciaBloqueio.Nenhuma, null, null);

        var bloqueado = await servico.EstaBloqueadoAsync(
            medicoId, new DateTime(2026, 7, 13, 14, 0, 0), new DateTime(2026, 7, 13, 14, 20, 0));

        Assert.False(bloqueado);
    }
}
