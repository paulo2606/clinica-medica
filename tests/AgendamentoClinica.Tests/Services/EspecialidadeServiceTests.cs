using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class EspecialidadeServiceTests
{
    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    [Fact]
    public async Task CriarAsync_ComNomeValido_DeveCriarEspecialidade()
    {
        var servico = new EspecialidadeService(CriarDbContext());

        var id = await servico.CriarAsync("Cardiologia");

        Assert.NotNull(id);
    }

    [Fact]
    public async Task CriarAsync_ComNomeJaExistente_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var servico = new EspecialidadeService(db);
        await servico.CriarAsync("Cardiologia");

        var id = await servico.CriarAsync("Cardiologia");

        Assert.Null(id);
    }

    [Fact]
    public async Task ListarAsync_SemIncluirInativas_NaoDeveRetornarDesativadas()
    {
        var db = CriarDbContext();
        var servico = new EspecialidadeService(db);
        var id = await servico.CriarAsync("Cardiologia");
        await servico.DesativarAsync(id!.Value);

        var lista = await servico.ListarAsync(incluirInativas: false);

        Assert.Empty(lista);
    }

    [Fact]
    public async Task ListarAsync_ComIncluirInativas_DeveRetornarDesativadas()
    {
        var db = CriarDbContext();
        var servico = new EspecialidadeService(db);
        var id = await servico.CriarAsync("Cardiologia");
        await servico.DesativarAsync(id!.Value);

        var lista = await servico.ListarAsync(incluirInativas: true);

        Assert.Single(lista);
    }

    [Fact]
    public async Task AtualizarAsync_ComIdInexistente_DeveRetornarNaoEncontrado()
    {
        var servico = new EspecialidadeService(CriarDbContext());

        var resultado = await servico.AtualizarAsync(Guid.NewGuid(), "Cardiologia");

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task AtualizarAsync_ParaNomeJaUsadoPorOutra_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var servico = new EspecialidadeService(db);
        await servico.CriarAsync("Cardiologia");
        var idPediatria = await servico.CriarAsync("Pediatria");

        var resultado = await servico.AtualizarAsync(idPediatria!.Value, "Cardiologia");

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
    }

    [Fact]
    public async Task AtualizarAsync_ComDadosValidos_DeveAtualizarNome()
    {
        var db = CriarDbContext();
        var servico = new EspecialidadeService(db);
        var id = await servico.CriarAsync("Cardiologia");

        var resultado = await servico.AtualizarAsync(id!.Value, "Cardiologia Pediátrica");

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var especialidade = await servico.ObterAsync(id.Value);
        Assert.Equal("Cardiologia Pediátrica", especialidade!.Nome);
    }

    [Fact]
    public async Task DesativarAsync_ComIdInexistente_DeveRetornarNaoEncontrado()
    {
        var servico = new EspecialidadeService(CriarDbContext());

        var resultado = await servico.DesativarAsync(Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task DesativarAsync_ComIdExistente_DeveMarcarComoInativa()
    {
        var db = CriarDbContext();
        var servico = new EspecialidadeService(db);
        var id = await servico.CriarAsync("Cardiologia");

        var resultado = await servico.DesativarAsync(id!.Value);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var especialidade = await servico.ObterAsync(id.Value);
        Assert.False(especialidade!.Ativo);
    }
}
