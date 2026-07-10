using System.Net;
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
public class AuthRateLimitTests : IClassFixture<RateLimitedWebApplicationFactory>, IAsyncLifetime
{
    private readonly RateLimitedWebApplicationFactory _factory;

    public AuthRateLimitTests(RateLimitedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        await db.Database.MigrateAsync();
        db.TokensRenovacao.RemoveRange(db.TokensRenovacao);
        db.Usuarios.RemoveRange(db.Usuarios);
        await db.SaveChangesAsync();

        var senhaService = escopo.ServiceProvider.GetRequiredService<ISenhaService>();
        db.Usuarios.Add(new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Carla Recepcao",
            Email = "carla@clinica.com",
            SenhaHash = senhaService.GerarHash("senha123"),
            Papel = PapelUsuario.Recepcao,
            Ativo = true
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_ComMuitasTentativas_DeveRetornar429()
    {
        var cliente = _factory.CreateClient();

        HttpResponseMessage? ultimaResposta = null;
        for (var i = 0; i < 4; i++)
        {
            ultimaResposta = await cliente.PostAsJsonAsync("/api/auth/login",
                new LoginRequest("carla@clinica.com", "senhaErrada"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultimaResposta!.StatusCode);
    }
}
