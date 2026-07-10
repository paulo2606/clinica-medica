using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class PacienteServiceTests
{
    private const string CpfValido = "11144477735";
    private const string CpfValido2 = "52998224725";
    private static readonly DateOnly DataNascimento = new(1990, 5, 20);

    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    [Fact]
    public async Task CriarAsync_ComDadosValidos_DeveCriarPacienteComCpfNormalizado()
    {
        var db = CriarDbContext();
        var servico = new PacienteService(db);

        var id = await servico.CriarAsync("Maria Silva", "111.444.777-35", "41988887777", "maria@email.com", DataNascimento);

        Assert.NotNull(id);
        var paciente = await servico.ObterAsync(id!.Value);
        Assert.Equal(CpfValido, paciente!.Cpf);
    }

    [Fact]
    public async Task CriarAsync_ComCpfJaCadastrado_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var servico = new PacienteService(db);
        await servico.CriarAsync("Maria Silva", CpfValido, "41988887777", null, DataNascimento);

        var id = await servico.CriarAsync("Outra Pessoa", "111.444.777-35", "41988880000", null, DataNascimento);

        Assert.Null(id);
    }

    [Fact]
    public async Task BuscarAsync_PorCpf_DeveEncontrarComOuSemFormatacao()
    {
        var db = CriarDbContext();
        var servico = new PacienteService(db);
        await servico.CriarAsync("Maria Silva", CpfValido, "41988887777", null, DataNascimento);

        var resultado = await servico.BuscarAsync(cpf: "111.444.777-35", nome: null, incluirInativos: false);

        Assert.Single(resultado);
    }

    [Fact]
    public async Task BuscarAsync_PorNomeParcial_DeveEncontrar()
    {
        var db = CriarDbContext();
        var servico = new PacienteService(db);
        await servico.CriarAsync("Maria Silva", CpfValido, "41988887777", null, DataNascimento);

        var resultado = await servico.BuscarAsync(cpf: null, nome: "mar", incluirInativos: false);

        Assert.Single(resultado);
    }

    [Fact]
    public async Task BuscarAsync_SemIncluirInativos_NaoDeveRetornarDesativados()
    {
        var db = CriarDbContext();
        var servico = new PacienteService(db);
        var id = await servico.CriarAsync("Maria Silva", CpfValido, "41988887777", null, DataNascimento);
        await servico.DesativarAsync(id!.Value);

        var resultado = await servico.BuscarAsync(cpf: null, nome: null, incluirInativos: false);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task AtualizarAsync_ComIdInexistente_DeveRetornarNaoEncontrado()
    {
        var servico = new PacienteService(CriarDbContext());

        var resultado = await servico.AtualizarAsync(Guid.NewGuid(), "Nome", CpfValido, "41988887777", null, DataNascimento);

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task AtualizarAsync_ParaCpfJaUsadoPorOutroPaciente_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var servico = new PacienteService(db);
        await servico.CriarAsync("Maria Silva", CpfValido, "41988887777", null, DataNascimento);
        var idOutro = await servico.CriarAsync("Outra Pessoa", CpfValido2, "41988880000", null, DataNascimento);

        var resultado = await servico.AtualizarAsync(idOutro!.Value, "Outra Pessoa", CpfValido, "41988880000", null, DataNascimento);

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
    }

    [Fact]
    public async Task DesativarAsync_ComIdExistente_DeveMarcarComoInativo()
    {
        var db = CriarDbContext();
        var servico = new PacienteService(db);
        var id = await servico.CriarAsync("Maria Silva", CpfValido, "41988887777", null, DataNascimento);

        var resultado = await servico.DesativarAsync(id!.Value);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var paciente = await servico.ObterAsync(id.Value);
        Assert.False(paciente!.Ativo);
    }
}
