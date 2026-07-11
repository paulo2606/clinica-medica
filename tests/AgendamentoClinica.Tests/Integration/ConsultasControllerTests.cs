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
public class ConsultasControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public ConsultasControllerTests(CustomWebApplicationFactory factory)
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
        db.HorariosTrabalhoMedico.RemoveRange(db.HorariosTrabalhoMedico);
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

    private async Task<Guid> CriarMedicoComHorarioAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        db.Especialidades.Add(especialidade);
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Bruno Medico",
            Email = $"{Guid.NewGuid()}@clinica.com",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            SenhaHash = "hash",
            Papel = PapelUsuario.Medico,
            Ativo = true
        };
        db.Usuarios.Add(usuario);
        var medico = new Medico { Id = Guid.NewGuid(), UsuarioId = usuario.Id, EspecialidadeId = especialidade.Id, Crm = $"CRM{Random.Shared.Next(1000000, 9999999)}" };
        db.Medicos.Add(medico);
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medico.Id,
            DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(9, 0)
        });
        await db.SaveChangesAsync();
        return medico.Id;
    }

    private async Task<Guid> CriarPacienteAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Paciente Teste",
            Cpf = $"{Random.Shared.NextInt64(10000000000, 99999999999)}",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            DataNascimento = new DateOnly(1990, 1, 1)
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        return paciente.Id;
    }

    [Fact]
    public async Task Criar_ComHorarioLivre_DeveRetornar201()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();
        var pacienteId = await CriarPacienteAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), "Primeira consulta"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ComHorarioJaOcupado_DeveRetornar400()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();
        var pacienteId = await CriarPacienteAsync();
        await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));

        var resposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 5, 0, DateTimeKind.Utc), null));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ComoMedico_DeveRetornar403()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Medico);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();
        var pacienteId = await CriarPacienteAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Cancelar_ComIdExistente_DeveRetornar204ELiberarHorario()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();
        var pacienteId = await CriarPacienteAsync();
        var criarResposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));
        var corpo = await criarResposta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();

        var resposta = await cliente.PatchAsync($"/api/consultas/{corpo!["id"]}/cancelar", null);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var novaConsultaResposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));
        Assert.Equal(HttpStatusCode.Created, novaConsultaResposta.StatusCode);
    }

    [Fact]
    public async Task Reagendar_ParaHorarioLivre_DeveRetornar204()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();
        var pacienteId = await CriarPacienteAsync();
        var criarResposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));
        var corpo = await criarResposta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();

        var resposta = await cliente.PatchAsJsonAsync($"/api/consultas/{corpo!["id"]}/reagendar",
            new ReagendarConsultaRequest(new DateTime(2026, 7, 13, 8, 40, 0, DateTimeKind.Utc)));

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
    }

    [Fact]
    public async Task HorariosLivres_ComoRecepcao_DeveRetornarSlots()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();

        var resposta = await cliente.GetAsync($"/api/consultas/horarios-livres?medicoId={medicoId}&data=2026-07-13");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var slots = await resposta.Content.ReadFromJsonAsync<List<DateTime>>();
        Assert.Equal(3, slots!.Count);
    }

    [Fact]
    public async Task HorariosLivres_ComoMedico_DeveRetornar403()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Medico);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();

        var resposta = await cliente.GetAsync($"/api/consultas/horarios-livres?medicoId={medicoId}&data=2026-07-13");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
