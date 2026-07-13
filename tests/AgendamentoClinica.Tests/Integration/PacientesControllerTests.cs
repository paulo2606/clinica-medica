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
public class PacientesControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly DateOnly DataNascimento = new(1990, 5, 20);

    public PacientesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        await db.Database.MigrateAsync();
        db.Consultas.RemoveRange(db.Consultas);
        db.Pacientes.RemoveRange(db.Pacientes);
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
    public async Task Criar_ComoRecepcao_DeveRetornar201()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/pacientes",
            new CriarPacienteRequest("Maria Silva", "111.444.777-35", "41988887777", null, DataNascimento));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ComoMedico_DeveRetornar403()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Medico);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/pacientes",
            new CriarPacienteRequest("Maria Silva", "111.444.777-35", "41988887777", null, DataNascimento));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ComCpfInvalido_DeveRetornar400()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Admin);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/pacientes",
            new CriarPacienteRequest("Maria Silva", "111.111.111-11", "41988887777", null, DataNascimento));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
