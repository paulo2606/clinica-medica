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
public class EspecialidadesControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public EspecialidadesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        await db.Database.MigrateAsync();
        db.Especialidades.RemoveRange(db.Especialidades);
        db.TokensRenovacao.RemoveRange(db.TokensRenovacao);
        db.Usuarios.RemoveRange(db.Usuarios);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> CriarUsuarioELogarAsync(HttpClient cliente, PapelUsuario papel)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var senhaService = escopo.ServiceProvider.GetRequiredService<ISenhaService>();
        var email = $"{Guid.NewGuid()}@clinica.com";
        db.Usuarios.Add(new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Usuário Teste",
            Email = email,
            SenhaHash = senhaService.GerarHash("senha123"),
            Papel = papel,
            Ativo = true
        });
        await db.SaveChangesAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "senha123"));
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return corpo!["accessToken"];
    }

    [Fact]
    public async Task Criar_ComoAdmin_DeveRetornar201()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Admin);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/especialidades", new CriarEspecialidadeRequest("Cardiologia"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ComoRecepcao_DeveRetornar403()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/especialidades", new CriarEspecialidadeRequest("Cardiologia"));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_SemToken_DeveRetornar401()
    {
        var cliente = _factory.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/especialidades", new CriarEspecialidadeRequest("Cardiologia"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Listar_ComoAdmin_DeveRetornarApenasAtivasPorPadrao()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Admin);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await cliente.PostAsJsonAsync("/api/especialidades", new CriarEspecialidadeRequest("Cardiologia"));

        var resposta = await cliente.GetAsync("/api/especialidades");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var lista = await resposta.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.Single(lista!);
    }
}
