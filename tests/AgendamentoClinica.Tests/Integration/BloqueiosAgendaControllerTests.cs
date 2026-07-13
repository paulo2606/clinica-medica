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
public class BloqueiosAgendaControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public BloqueiosAgendaControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        await db.Database.MigrateAsync();
        db.BloqueiosAgendaMedico.RemoveRange(db.BloqueiosAgendaMedico);
        db.Consultas.RemoveRange(db.Consultas);
        db.Medicos.RemoveRange(db.Medicos);
        db.Especialidades.RemoveRange(db.Especialidades);
        db.TokensRenovacao.RemoveRange(db.TokensRenovacao);
        db.Usuarios.RemoveRange(db.Usuarios);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> LogarAsync(HttpClient cliente, string email, string senha)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, senha));
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return corpo!["accessToken"];
    }

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

        return await LogarAsync(cliente, email, "senha123");
    }

    private async Task<(Guid MedicoId, string Token)> CriarMedicoELogarAsync(HttpClient cliente)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var senhaService = escopo.ServiceProvider.GetRequiredService<ISenhaService>();
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        db.Especialidades.Add(especialidade);
        var email = $"{Guid.NewGuid()}@clinica.com";
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Bruno Medico",
            Email = email,
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            SenhaHash = senhaService.GerarHash("senha123"),
            Papel = PapelUsuario.Medico,
            Ativo = true
        };
        db.Usuarios.Add(usuario);
        var medico = new Medico { Id = Guid.NewGuid(), UsuarioId = usuario.Id, EspecialidadeId = especialidade.Id, Crm = $"CRM{Random.Shared.Next(1000000, 9999999)}" };
        db.Medicos.Add(medico);
        await db.SaveChangesAsync();

        var token = await LogarAsync(cliente, email, "senha123");
        return (medico.Id, token);
    }

    [Fact]
    public async Task CriarMeu_ComoMedico_DeveRetornar201EAssociarAoProprioMedico()
    {
        var cliente = _factory.CreateClient();
        var (medicoId, token) = await CriarMedicoELogarAsync(cliente);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await cliente.PostAsJsonAsync("/api/bloqueios-agenda/meus",
            new CriarMeuBloqueioRequest(new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
                TipoRecorrenciaBloqueio.Nenhuma, null, "Férias"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var bloqueio = await db.BloqueiosAgendaMedico.SingleAsync();
        Assert.Equal(medicoId, bloqueio.MedicoId);
    }

    [Fact]
    public async Task Criar_ComoRecepcao_ParaOutroMedico_DeveRetornar201()
    {
        var cliente = _factory.CreateClient();
        var (medicoId, _) = await CriarMedicoELogarAsync(cliente);
        var tokenRecepcao = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRecepcao);

        var resposta = await cliente.PostAsJsonAsync("/api/bloqueios-agenda",
            new CriarBloqueioAgendaRequest(medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
                TipoRecorrenciaBloqueio.Nenhuma, null, "Feriado"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Atualizar_ComoMedicoDeOutraPessoa_DeveRetornar404()
    {
        var cliente = _factory.CreateClient();
        var (_, tokenDono) = await CriarMedicoELogarAsync(cliente);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDono);
        var criarResposta = await cliente.PostAsJsonAsync("/api/bloqueios-agenda/meus",
            new CriarMeuBloqueioRequest(new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
                TipoRecorrenciaBloqueio.Nenhuma, null, "Férias"));
        var corpo = await criarResposta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var bloqueioId = corpo!["id"];

        var (_, tokenOutroMedico) = await CriarMedicoELogarAsync(cliente);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOutroMedico);

        var resposta = await cliente.PutAsJsonAsync($"/api/bloqueios-agenda/{bloqueioId}",
            new AtualizarBloqueioAgendaRequest(new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc),
                TipoRecorrenciaBloqueio.Nenhuma, null, "Tentativa indevida"));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Remover_ComoDono_DeveRetornar204()
    {
        var cliente = _factory.CreateClient();
        var (_, token) = await CriarMedicoELogarAsync(cliente);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var criarResposta = await cliente.PostAsJsonAsync("/api/bloqueios-agenda/meus",
            new CriarMeuBloqueioRequest(new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
                TipoRecorrenciaBloqueio.Nenhuma, null, "Férias"));
        var corpo = await criarResposta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var bloqueioId = corpo!["id"];

        var resposta = await cliente.DeleteAsync($"/api/bloqueios-agenda/{bloqueioId}");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
    }

    [Fact]
    public async Task Listar_ComoRecepcao_DeveRetornar200()
    {
        var cliente = _factory.CreateClient();
        var (medicoId, _) = await CriarMedicoELogarAsync(cliente);
        var tokenRecepcao = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRecepcao);

        var resposta = await cliente.GetAsync($"/api/bloqueios-agenda?medicoId={medicoId}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task ListarMeus_ComoRecepcao_DeveRetornar403()
    {
        var cliente = _factory.CreateClient();
        var tokenRecepcao = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRecepcao);

        var resposta = await cliente.GetAsync("/api/bloqueios-agenda/meus");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
