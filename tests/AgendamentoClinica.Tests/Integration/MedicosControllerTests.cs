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
public class MedicosControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public MedicosControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        await db.Database.MigrateAsync();
        db.TokensConviteSenha.RemoveRange(db.TokensConviteSenha);
        db.Consultas.RemoveRange(db.Consultas);
        db.Medicos.RemoveRange(db.Medicos);
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
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            SenhaHash = senhaService.GerarHash("senha123"),
            Papel = papel,
            Ativo = true
        });
        await db.SaveChangesAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "senha123"));
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return corpo!["accessToken"];
    }

    private async Task<Guid> CriarEspecialidadeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        db.Especialidades.Add(especialidade);
        await db.SaveChangesAsync();
        return especialidade.Id;
    }

    [Fact]
    public async Task Criar_ComoAdmin_DeveRetornar201EGerarTokenDeConvite()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Admin);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var especialidadeId = await CriarEspecialidadeAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/medicos",
            new CriarMedicoRequest("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        Assert.True(await db.TokensConviteSenha.AnyAsync());
    }

    [Fact]
    public async Task Criar_ComoRecepcao_DeveRetornar403()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var especialidadeId = await CriarEspecialidadeAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/medicos",
            new CriarMedicoRequest("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", especialidadeId));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Listar_ComoRecepcao_DeveRetornar200()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.GetAsync("/api/medicos");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Listar_ComoMedico_DeveRetornar403()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Medico);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.GetAsync("/api/medicos");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ComEspecialidadeInexistente_DeveRetornar400()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Admin);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/medicos",
            new CriarMedicoRequest("Bruno Medico", "bruno@clinica.com", "41988887777", "CRM12345", Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
