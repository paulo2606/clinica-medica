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

    private class FilaEmailFake : IFilaEmail
    {
        public List<EmailMensagem> Enfileiradas { get; } = [];
        public void Enfileirar(EmailMensagem mensagem) => Enfileiradas.Add(mensagem);
    }

    private static AuthService CriarServico(AgendamentoDbContext db, ISenhaService senhaService, IFilaEmail? filaEmail = null)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:UrlBase"] = "http://localhost:3000" })
            .Build();
        return new AuthService(db, senhaService, CriarTokenService(), filaEmail ?? new FilaEmailFake(), configuracao);
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
        var servico = CriarServico(db, senhaService);

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
        var servico = CriarServico(db, senhaService);

        var resultado = await servico.AutenticarAsync("ana@clinica.com", "senhaErrada");

        Assert.Null(resultado);
    }

    [Fact]
    public async Task AutenticarAsync_ComEmailInexistente_DeveChamarVerificarMesmoAssim()
    {
        var db = CriarDbContext();
        var senhaServiceFake = new SenhaServiceContadora();
        var servico = CriarServico(db, senhaServiceFake);

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
        var servico = CriarServico(db, senhaService);

        var resultado = await servico.AutenticarAsync("ana@clinica.com", "senha123");

        Assert.Null(resultado);
    }

    [Fact]
    public async Task RenovarAsync_ComRefreshTokenValido_DeveRotacionarERetornarNovosTokens()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        await CriarUsuarioAsync(db, senhaService);
        var servico = CriarServico(db, senhaService);
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
        var servico = CriarServico(db, senhaService);
        var login = await servico.AutenticarAsync("ana@clinica.com", "senha123");

        await servico.RevogarAsync(login!.RefreshToken);
        var renovado = await servico.RenovarAsync(login.RefreshToken);

        Assert.Null(renovado);
    }

    [Fact]
    public async Task CadastrarAsync_ComDadosValidos_DeveCriarUsuario()
    {
        var db = CriarDbContext();
        var servico = CriarServico(db, new SenhaService());

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
        var servico = CriarServico(db, senhaService);

        var id = await servico.CadastrarAsync("Outra Pessoa", "ana@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Recepcao);

        Assert.Null(id);
    }

    [Fact]
    public async Task CadastrarAsync_ComTelefoneJaExistente_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var servico = CriarServico(db, new SenhaService());
        await servico.CadastrarAsync("Bruno Medico", "bruno@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Medico);

        var id = await servico.CadastrarAsync("Outra Pessoa", "outra@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Recepcao);

        Assert.Null(id);
    }

    [Fact]
    public async Task CadastrarAsync_ComSenhaCurta_DeveRetornarNulo()
    {
        var db = CriarDbContext();
        var servico = CriarServico(db, new SenhaService());

        var id = await servico.CadastrarAsync("Bruno Medico", "bruno@clinica.com", "1234567", "11988887777", PapelUsuario.Medico);

        Assert.Null(id);
    }

    [Fact]
    public async Task EsqueciSenhaAsync_ComEmailExistente_DeveEnfileirarEmailEGerarToken()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        var usuario = await CriarUsuarioAsync(db, senhaService);
        var fila = new FilaEmailFake();
        var servico = CriarServico(db, senhaService, fila);

        await servico.EsqueciSenhaAsync("ana@clinica.com");

        Assert.Single(fila.Enfileiradas);
        Assert.Equal("ana@clinica.com", fila.Enfileiradas[0].Para);
        Assert.True(await db.TokensConviteSenha.AnyAsync(t => t.UsuarioId == usuario.Id && t.UsadoEm == null));
    }

    [Fact]
    public async Task EsqueciSenhaAsync_ComEmailInexistente_NaoDeveEnfileirarNada()
    {
        var db = CriarDbContext();
        var fila = new FilaEmailFake();
        var servico = CriarServico(db, new SenhaService(), fila);

        await servico.EsqueciSenhaAsync("naoexiste@clinica.com");

        Assert.Empty(fila.Enfileiradas);
    }

    [Fact]
    public async Task EsqueciSenhaAsync_ChamadoDuasVezes_DeveInvalidarTokenAnterior()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        var usuario = await CriarUsuarioAsync(db, senhaService);
        var servico = CriarServico(db, senhaService);

        await servico.EsqueciSenhaAsync("ana@clinica.com");
        await servico.EsqueciSenhaAsync("ana@clinica.com");

        var tokens = await db.TokensConviteSenha.Where(t => t.UsuarioId == usuario.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.UsadoEm == null);
    }

    [Fact]
    public async Task DefinirSenhaAsync_ComTokenValido_DeveTrocarSenhaEPermitirLogin()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        var usuario = await CriarUsuarioAsync(db, senhaService);
        var tokenService = CriarTokenService();
        var tokenBruto = tokenService.GerarRefreshTokenBruto();
        db.TokensConviteSenha.Add(new TokenConviteSenha
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = tokenService.HashToken(tokenBruto),
            ExpiraEm = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();
        var servico = CriarServico(db, senhaService);

        var resultado = await servico.DefinirSenhaAsync(tokenBruto, "novaSenhaForte123");

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var login = await servico.AutenticarAsync("ana@clinica.com", "novaSenhaForte123");
        Assert.NotNull(login);
    }

    [Fact]
    public async Task DefinirSenhaAsync_ComTokenInvalido_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var servico = CriarServico(db, new SenhaService());

        var resultado = await servico.DefinirSenhaAsync("token-invalido", "novaSenhaForte123");

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task DefinirSenhaAsync_ComTokenJaUsado_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        var usuario = await CriarUsuarioAsync(db, senhaService);
        var tokenService = CriarTokenService();
        var tokenBruto = tokenService.GerarRefreshTokenBruto();
        db.TokensConviteSenha.Add(new TokenConviteSenha
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = tokenService.HashToken(tokenBruto),
            ExpiraEm = DateTime.UtcNow.AddHours(1),
            UsadoEm = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var servico = CriarServico(db, senhaService);

        var resultado = await servico.DefinirSenhaAsync(tokenBruto, "novaSenhaForte123");

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task DefinirSenhaAsync_DeveRevogarRefreshTokensExistentes()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        var usuario = await CriarUsuarioAsync(db, senhaService);
        var servico = CriarServico(db, senhaService);
        var login = await servico.AutenticarAsync("ana@clinica.com", "senha123");
        var tokenService = CriarTokenService();
        var tokenBruto = tokenService.GerarRefreshTokenBruto();
        db.TokensConviteSenha.Add(new TokenConviteSenha
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = tokenService.HashToken(tokenBruto),
            ExpiraEm = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();

        await servico.DefinirSenhaAsync(tokenBruto, "novaSenhaForte123");
        var renovado = await servico.RenovarAsync(login!.RefreshToken);

        Assert.Null(renovado);
    }

    [Fact]
    public async Task TrocarSenhaAsync_ComSenhaAtualCorreta_DeveTrocarERevogarSessoes()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        var usuario = await CriarUsuarioAsync(db, senhaService);
        var servico = CriarServico(db, senhaService);
        var login = await servico.AutenticarAsync("ana@clinica.com", "senha123");

        var sucesso = await servico.TrocarSenhaAsync(usuario.Id, "senha123", "novaSenhaForte123");

        Assert.True(sucesso);
        var loginNovo = await servico.AutenticarAsync("ana@clinica.com", "novaSenhaForte123");
        Assert.NotNull(loginNovo);
        var renovado = await servico.RenovarAsync(login!.RefreshToken);
        Assert.Null(renovado);
    }

    [Fact]
    public async Task TrocarSenhaAsync_ComSenhaAtualErrada_DeveRetornarFalse()
    {
        var db = CriarDbContext();
        var senhaService = new SenhaService();
        var usuario = await CriarUsuarioAsync(db, senhaService);
        var servico = CriarServico(db, senhaService);

        var sucesso = await servico.TrocarSenhaAsync(usuario.Id, "senhaErrada", "novaSenhaForte123");

        Assert.False(sucesso);
    }
}
