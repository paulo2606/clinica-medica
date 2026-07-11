using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class HorarioTrabalhoServiceTests
{
    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    private static async Task<Guid> CriarMedicoAsync(AgendamentoDbContext db, bool ativo = true)
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
            Ativo = ativo
        };
        db.Medicos.Add(medico);
        await db.SaveChangesAsync();
        return medico.Id;
    }

    [Fact]
    public async Task CriarAsync_ComDadosValidos_DeveCriarHorario()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);

        var (resultado, id) = await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.NotNull(id);
    }

    [Fact]
    public async Task CriarAsync_ComMedicoInexistente_DeveRetornarNaoEncontrado()
    {
        var servico = new HorarioTrabalhoService(CriarDbContext());

        var (resultado, id) = await servico.CriarAsync(Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CriarAsync_ComMedicoInativo_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db, ativo: false);
        var servico = new HorarioTrabalhoService(db);

        var (resultado, id) = await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CriarAsync_ComSobreposicaoNoMesmoDia_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);
        await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var (resultado, id) = await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(14, 0));

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CriarAsync_SemSobreposicaoNoMesmoDia_DeveCriar()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);
        await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var (resultado, id) = await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0));

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.NotNull(id);
    }

    [Fact]
    public async Task CriarAsync_MesmoHorarioEmDiaDiferente_DeveCriar()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);
        await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var (resultado, id) = await servico.CriarAsync(medicoId, DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.NotNull(id);
    }

    [Fact]
    public async Task ListarPorMedicoAsync_DeveRetornarOrdenadoPorDiaEHora()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);
        await servico.CriarAsync(medicoId, DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(12, 0));
        await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0));
        await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var lista = await servico.ListarPorMedicoAsync(medicoId);

        Assert.Equal(3, lista.Count);
        Assert.Equal(DayOfWeek.Monday, lista[0].DiaSemana);
        Assert.Equal(new TimeOnly(8, 0), lista[0].HoraInicio);
        Assert.Equal(DayOfWeek.Monday, lista[1].DiaSemana);
        Assert.Equal(new TimeOnly(14, 0), lista[1].HoraInicio);
        Assert.Equal(DayOfWeek.Tuesday, lista[2].DiaSemana);
    }

    [Fact]
    public async Task AtualizarAsync_ComIdInexistente_DeveRetornarNaoEncontrado()
    {
        var servico = new HorarioTrabalhoService(CriarDbContext());

        var resultado = await servico.AtualizarAsync(Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task AtualizarAsync_ParaSobreporOutroHorario_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);
        await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var (_, idOutro) = await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0));

        var resultado = await servico.AtualizarAsync(idOutro!.Value, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(15, 0));

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
    }

    [Fact]
    public async Task AtualizarAsync_ComDadosValidos_DeveAtualizar()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);
        var (_, id) = await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var resultado = await servico.AtualizarAsync(id!.Value, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0));

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var lista = await servico.ListarPorMedicoAsync(medicoId);
        Assert.Equal(new TimeOnly(9, 0), lista.Single().HoraInicio);
    }

    [Fact]
    public async Task RemoverAsync_ComIdExistente_DeveRemover()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new HorarioTrabalhoService(db);
        var (_, id) = await servico.CriarAsync(medicoId, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var resultado = await servico.RemoverAsync(id!.Value);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.Empty(await servico.ListarPorMedicoAsync(medicoId));
    }

    [Fact]
    public async Task RemoverAsync_ComIdInexistente_DeveRetornarNaoEncontrado()
    {
        var servico = new HorarioTrabalhoService(CriarDbContext());

        var resultado = await servico.RemoverAsync(Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }
}
