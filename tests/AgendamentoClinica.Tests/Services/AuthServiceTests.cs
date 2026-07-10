using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class AuthServiceTests
{
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

    private static async Task<Usuario> CriarUsuarioAsync(AgendamentoDbContext db, ISenhaService senhaService, bool ativo = true)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Ana Recepcionista",
            Email = "ana@clinica.com",
            SenhaHash = senhaService.GerarHash("senha123"),
            Papel = PapelUsuario.Recepcao,
            Ativo = ativo
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task AutenticarAsync_ComCredenciaisValidas_DeveRetornarTokens()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        await CriarUsuarioAsync(db, senhaService);
        var servico = new AuthService(db, senhaService, CriarTokenService());

        var resultado = await servico.AutenticarAsync("ana@clinica.com", "senha123");

        Assert.NotNull(resultado);
        Assert.NotEmpty(resultado!.AccessToken);
        Assert.NotEmpty(resultado.RefreshToken);
    }

    [Fact]
    public async Task AutenticarAsync_ComSenhaIncorreta_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        await CriarUsuarioAsync(db, senhaService);
        var servico = new AuthService(db, senhaService, CriarTokenService());

        var resultado = await servico.AutenticarAsync("ana@clinica.com", "senhaErrada");

        Assert.Null(resultado);
    }

    [Fact]
    public async Task AutenticarAsync_ComEmailInexistente_DeveChamarVerificarMesmoAssim()
    {
        // Prova a correção do timing attack: mesmo sem usuário, o hash precisa
        // ser comparado, senão o tempo de resposta denuncia que o e-mail não existe.
        var db = CriarDbContext();
        var senhaServiceFake = new SenhaServiceContadora();
        var servico = new AuthService(db, senhaServiceFake, CriarTokenService());

        var resultado = await servico.AutenticarAsync("naoexiste@clinica.com", "qualquerSenha");

        Assert.Null(resultado);
        Assert.Equal(1, senhaServiceFake.ChamadasVerificar);
    }

    private class SenhaServiceContadora : ISenhaService
    {
        public int ChamadasVerificar { get; private set; }

        public string GerarHash(string senha) => new SenhaService().GerarHash(senha);

        public bool Verificar(string senha, string hash)
        {
            ChamadasVerificar++;
            return false;
        }
    }

    [Fact]
    public async Task AutenticarAsync_ComUsuarioInativo_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        await CriarUsuarioAsync(db, senhaService, ativo: false);
        var servico = new AuthService(db, senhaService, CriarTokenService());

        var resultado = await servico.AutenticarAsync("ana@clinica.com", "senha123");

        Assert.Null(resultado);
    }

    [Fact]
    public async Task RenovarAsync_ComRefreshTokenValido_DeveRotacionarERetornarNovosTokens()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        await CriarUsuarioAsync(db, senhaService);
        var servico = new AuthService(db, senhaService, CriarTokenService());
        var login = await servico.AutenticarAsync("ana@clinica.com", "senha123");

        var renovado = await servico.RenovarAsync(login!.RefreshToken);

        Assert.NotNull(renovado);
        Assert.NotEqual(login.RefreshToken, renovado!.RefreshToken);
        var reuso = await servico.RenovarAsync(login.RefreshToken);
        Assert.Null(reuso);
    }

    [Fact]
    public async Task RevogarAsync_TokenRevogado_NaoDeveMaisRenovar()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        await CriarUsuarioAsync(db, senhaService);
        var servico = new AuthService(db, senhaService, CriarTokenService());
        var login = await servico.AutenticarAsync("ana@clinica.com", "senha123");

        await servico.RevogarAsync(login!.RefreshToken);
        var renovado = await servico.RenovarAsync(login.RefreshToken);

        Assert.Null(renovado);
    }

    [Fact]
    public async Task CadastrarAsync_ComDadosValidos_DeveCriarUsuario()
    {
        var db = CriarDbContext();
        var servico = new AuthService(db, new SenhaService(), CriarTokenService());

        var id = await servico.CadastrarAsync("Bruno Medico", "bruno@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Medico);

        Assert.NotNull(id);
        var usuario = await db.Usuarios.FindAsync(id);
        Assert.NotNull(usuario);
        Assert.NotEqual("senhaForte123", usuario!.SenhaHash);
    }

    [Fact]
    public async Task CadastrarAsync_ComEmailJaExistente_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        await CriarUsuarioAsync(db, senhaService);
        var servico = new AuthService(db, senhaService, CriarTokenService());

        var id = await servico.CadastrarAsync("Outra Pessoa", "ana@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Recepcao);

        Assert.Null(id);
    }

    [Fact]
    public async Task CadastrarAsync_ComSenhaCurta_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var servico = new AuthService(db, new SenhaService(), CriarTokenService());

        var id = await servico.CadastrarAsync("Bruno Medico", "bruno@clinica.com", "1234567", "11988887777", PapelUsuario.Medico);

        Assert.Null(id);
    }
}
