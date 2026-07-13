using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgendamentoClinica.Tests.Integration;

[Collection("BancoDeTeste")]
public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        await db.Database.MigrateAsync();
        db.TokensConviteSenha.RemoveRange(db.TokensConviteSenha);
        db.TokensRenovacao.RemoveRange(db.TokensRenovacao);
        db.Consultas.RemoveRange(db.Consultas);
        db.Usuarios.RemoveRange(db.Usuarios);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CriarUsuarioAsync(string email, string senha, PapelUsuario papel)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var senhaService = escopo.ServiceProvider.GetRequiredService<ISenhaService>();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Usuário Teste",
            Email = email,
            SenhaHash = senhaService.GerarHash(senha),
            Papel = papel,
            Ativo = true
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario.Id;
    }

    private async Task<string> LogarEObterTokenAsync(HttpClient cliente, string email, string senha)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, senha));
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return corpo!["accessToken"];
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_DeveRetornarAccessTokenECookieRefresh()
    {
        await CriarUsuarioAsync("ana@clinica.com", "senha123", PapelUsuario.Recepcao);
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("ana@clinica.com", "senha123"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.False(string.IsNullOrEmpty(corpo!["accessToken"]));
        Assert.Contains(resposta.Headers.GetValues("Set-Cookie"),
            c => c.Contains("refreshToken") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_ComSenhaIncorreta_DeveRetornar401ComMensagemGenerica()
    {
        await CriarUsuarioAsync("ana@clinica.com", "senha123", PapelUsuario.Recepcao);
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("ana@clinica.com", "senhaErrada"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Eu_ComTokenValido_DeveRetornarDadosDoUsuario()
    {
        var usuarioId = await CriarUsuarioAsync("bruno@clinica.com", "senha123", PapelUsuario.Medico);
        var cliente = _factory.CreateClient();
        var token = await LogarEObterTokenAsync(cliente, "bruno@clinica.com", "senha123");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(usuarioId.ToString(), corpo!["id"]);
        Assert.Equal("Medico", corpo!["papel"]);
    }

    [Fact]
    public async Task Eu_SemToken_DeveRetornar401()
    {
        var cliente = _factory.CreateClient();

        var resposta = await cliente.GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Cadastro_ComoAdmin_DeveCriarUsuario()
    {
        await CriarUsuarioAsync("admin@clinica.com", "senhaAdmin123", PapelUsuario.Admin);
        var cliente = _factory.CreateClient();
        var token = await LogarEObterTokenAsync(cliente, "admin@clinica.com", "senhaAdmin123");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/auth/cadastro",
            new CriarUsuarioRequest("Carla Recepcao", "carla@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Recepcao));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Cadastro_ComoRecepcao_DeveRetornar403()
    {
        await CriarUsuarioAsync("recepcao@clinica.com", "senha123", PapelUsuario.Recepcao);
        var cliente = _factory.CreateClient();
        var token = await LogarEObterTokenAsync(cliente, "recepcao@clinica.com", "senha123");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/auth/cadastro",
            new CriarUsuarioRequest("Outra Pessoa", "outra@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Recepcao));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Cadastro_SemToken_DeveRetornar401()
    {
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/cadastro",
            new CriarUsuarioRequest("Outra Pessoa", "outra@clinica.com", "senhaForte123", "11988887777", PapelUsuario.Recepcao));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task EsqueciSenha_ComEmailExistente_DeveRetornar200()
    {
        await CriarUsuarioAsync("ana@clinica.com", "senha123", PapelUsuario.Recepcao);
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new EsqueciSenhaRequest("ana@clinica.com"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task EsqueciSenha_ComEmailInexistente_DeveRetornarMesmoStatusEMensagem()
    {
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new EsqueciSenhaRequest("naoexiste@clinica.com"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task DefinirSenha_ComTokenValido_DevePermitirLoginComNovaSenha()
    {
        var usuarioId = await CriarUsuarioAsync("ana@clinica.com", "senha123", PapelUsuario.Recepcao);
        var cliente = _factory.CreateClient();

        using (var escopo = _factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
            var tokenService = escopo.ServiceProvider.GetRequiredService<ITokenService>();
            var tokenBruto = tokenService.GerarRefreshTokenBruto();
            db.TokensConviteSenha.Add(new TokenConviteSenha
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                TokenHash = tokenService.HashToken(tokenBruto),
                ExpiraEm = DateTime.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync();

            var resposta = await cliente.PostAsJsonAsync("/api/auth/definir-senha",
                new DefinirSenhaRequest(tokenBruto, "novaSenhaForte123"));
            Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        }

        var loginResposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest("ana@clinica.com", "novaSenhaForte123"));
        Assert.Equal(HttpStatusCode.OK, loginResposta.StatusCode);
    }

    [Fact]
    public async Task DefinirSenha_ComTokenInvalido_DeveRetornar400()
    {
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/definir-senha",
            new DefinirSenhaRequest("token-invalido", "novaSenhaForte123"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task TrocarSenha_ComSenhaAtualCorreta_DevePermitirLoginComNovaSenha()
    {
        await CriarUsuarioAsync("ana@clinica.com", "senha123", PapelUsuario.Recepcao);
        var cliente = _factory.CreateClient();
        var token = await LogarEObterTokenAsync(cliente, "ana@clinica.com", "senha123");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PutAsJsonAsync("/api/auth/senha", new TrocarSenhaRequest("senha123", "novaSenhaForte123"));

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var loginResposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest("ana@clinica.com", "novaSenhaForte123"));
        Assert.Equal(HttpStatusCode.OK, loginResposta.StatusCode);
    }

    [Fact]
    public async Task TrocarSenha_ComSenhaAtualErrada_DeveRetornar400()
    {
        await CriarUsuarioAsync("ana@clinica.com", "senha123", PapelUsuario.Recepcao);
        var cliente = _factory.CreateClient();
        var token = await LogarEObterTokenAsync(cliente, "ana@clinica.com", "senha123");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PutAsJsonAsync("/api/auth/senha", new TrocarSenhaRequest("senhaErrada", "novaSenhaForte123"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task TrocarSenha_SemToken_DeveRetornar401()
    {
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PutAsJsonAsync("/api/auth/senha", new TrocarSenhaRequest("senha123", "novaSenhaForte123"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
