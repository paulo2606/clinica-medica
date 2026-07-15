using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class MedicoServiceTests
{
    private class FilaEmailFake : IFilaEmail
    {
        public List<EmailMensagem> Enfileiradas { get; } = [];
        public void Enfileirar(EmailMensagem mensagem) => Enfileiradas.Add(mensagem);
    }

    private class AmbienteFake : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "agendamento-testes", Guid.NewGuid().ToString());
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Testes";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testes";
    }

    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    private static TokenService CriarTokenService()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "chave-de-teste-com-pelo-menos-32-bytes-1234",
                ["Jwt:Issuer"] = "AgendamentoClinica",
                ["Jwt:Audience"] = "AgendamentoClinica.Cliente"
            })
            .Build();
        return new TokenService(configuracao);
    }

    private static async Task<Guid> CriarEspecialidadeAsync(AgendamentoDbContext db, string nome = "Cardiologia", bool ativa = true)
    {
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = nome, Ativo = ativa };
        db.Especialidades.Add(especialidade);
        await db.SaveChangesAsync();
        return especialidade.Id;
    }

    private static (MedicoService Servico, FilaEmailFake Fila) CriarServico(AgendamentoDbContext db)
    {
        var fila = new FilaEmailFake();
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:UrlBase"] = "http://localhost:3000" })
            .Build();
        var servico = new MedicoService(db, new SenhaService(), CriarTokenService(), fila, configuracao, new AmbienteFake());
        return (servico, fila);
    }

    [Fact]
    public async Task CriarAsync_ComDadosValidos_DeveCriarUsuarioMedicoEEnfileirarConvite()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, fila) = CriarServico(db);

        var (resultado, id) = await servico.CriarAsync(
            "Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.NotNull(id);

        var medico = await db.Medicos.Include(m => m.Usuario).FirstAsync(m => m.Id == id);
        Assert.Equal("bruno@clinica.com", medico.Usuario!.Email);
        Assert.Equal(PapelUsuario.Medico, medico.Usuario.Papel);

        Assert.Single(fila.Enfileiradas);
        Assert.Equal("bruno@clinica.com", fila.Enfileiradas[0].Para);
        Assert.Contains("definir-senha?token=", fila.Enfileiradas[0].Corpo);

        var tokenConvite = await db.TokensConviteSenha.SingleAsync(t => t.UsuarioId == medico.UsuarioId);
        Assert.Null(tokenConvite.UsadoEm);
        Assert.True(tokenConvite.ExpiraEm > DateTime.UtcNow);
    }

    [Fact]
    public async Task CriarAsync_ComEspecialidadeInexistente_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var (servico, fila) = CriarServico(db);

        var (resultado, id) = await servico.CriarAsync(
            "Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(id);
        Assert.Empty(fila.Enfileiradas);
    }

    [Fact]
    public async Task CriarAsync_ComEspecialidadeInativa_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db, ativa: false);
        var (servico, _) = CriarServico(db);

        var (resultado, id) = await servico.CriarAsync(
            "Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CriarAsync_ComEmailJaExistente_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, _) = CriarServico(db);
        await servico.CriarAsync("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);

        var (resultado, id) = await servico.CriarAsync(
            "Outro Medico", "bruno@clinica.com", "41988880000", "CRM99999", especialidadeId);

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CriarAsync_ComCrmJaExistente_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, _) = CriarServico(db);
        await servico.CriarAsync("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);

        var (resultado, id) = await servico.CriarAsync(
            "Outro Medico", "outro@clinica.com", "41988880000", "CRM12345", especialidadeId);

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task AtualizarAsync_ComCrmJaUsadoPorOutroMedico_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, _) = CriarServico(db);
        await servico.CriarAsync("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);
        var (_, idOutro) = await servico.CriarAsync("Outro Medico", "outro@clinica.com", "41988880000", "CRM99999", especialidadeId);

        var resultado = await servico.AtualizarAsync(idOutro!.Value, "CRM12345", especialidadeId);

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
    }

    [Fact]
    public async Task AtualizarMeuPerfilAsync_ComDadosValidos_DeveAtualizarNomeEEspecialidade()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db, "Cardiologia");
        var outraEspecialidadeId = await CriarEspecialidadeAsync(db, "Dermatologia");
        var (servico, _) = CriarServico(db);
        var (_, id) = await servico.CriarAsync("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);

        var resultado = await servico.AtualizarMeuPerfilAsync(id!.Value, "Bruno Atualizado", outraEspecialidadeId);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var medico = await servico.ObterAsync(id.Value);
        Assert.Equal("Bruno Atualizado", medico!.Usuario!.Nome);
        Assert.Equal(outraEspecialidadeId, medico.EspecialidadeId);
    }

    [Fact]
    public async Task AtualizarMeuPerfilAsync_ComEspecialidadeInexistente_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, _) = CriarServico(db);
        var (_, id) = await servico.CriarAsync("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);

        var resultado = await servico.AtualizarMeuPerfilAsync(id!.Value, "Bruno Atualizado", Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        var medico = await servico.ObterAsync(id.Value);
        Assert.Equal("Bruno Medico", medico!.Usuario!.Nome);
    }

    [Fact]
    public async Task AtualizarMeuPerfilAsync_ComIdInexistente_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, _) = CriarServico(db);

        var resultado = await servico.AtualizarMeuPerfilAsync(Guid.NewGuid(), "Bruno Atualizado", especialidadeId);

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task AtualizarFotoPerfilAsync_ComDadosValidos_DeveSalvarArquivoEAtualizarFotoUrl()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, _) = CriarServico(db);
        var (_, id) = await servico.CriarAsync("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);
        var conteudo = new byte[] { 0xFF, 0xD8, 0xFF, 0x01, 0x02 };

        var resultado = await servico.AtualizarFotoPerfilAsync(id!.Value, conteudo, "jpg");

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var medico = await servico.ObterAsync(id.Value);
        Assert.Equal($"/fotos-perfil/{medico!.Usuario!.Id}.jpg", medico.Usuario.FotoUrl);
    }

    [Fact]
    public async Task AtualizarFotoPerfilAsync_ComIdInexistente_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var (servico, _) = CriarServico(db);

        var resultado = await servico.AtualizarFotoPerfilAsync(Guid.NewGuid(), [0xFF, 0xD8, 0xFF], "jpg");

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task DesativarAsync_ComIdExistente_DeveMarcarComoInativo()
    {
        var db = CriarDbContext();
        var especialidadeId = await CriarEspecialidadeAsync(db);
        var (servico, _) = CriarServico(db);
        var (_, id) = await servico.CriarAsync("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId);

        var resultado = await servico.DesativarAsync(id!.Value);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var medico = await servico.ObterAsync(id.Value);
        Assert.False(medico!.Ativo);
    }
}
