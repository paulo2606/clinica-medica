using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "senha123"));
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return (medico.Id, corpo!["accessToken"]);
    }

    private async Task<(Guid MedicoId, string Token)> CriarMedicoComHorarioELogarAsync(HttpClient cliente)
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
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medico.Id,
            DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(10, 0)
        });
        await db.SaveChangesAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "senha123"));
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return (medico.Id, corpo!["accessToken"]);
    }

    private async Task<(Guid MedicoId, string Token)> CriarMedicoComHorarioEmDiaELogarAsync(HttpClient cliente, DiaSemana diaSemana)
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
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medico.Id,
            DiaSemana = diaSemana,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(9, 0)
        });
        await db.SaveChangesAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "senha123"));
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return (medico.Id, corpo!["accessToken"]);
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
    public async Task Criar_ComoMedico_DeveAgendarParaSiMesmoIgnorandoMedicoIdDoCorpo()
    {
        var cliente = _factory.CreateClient();
        var (medicoId, tokenMedico) = await CriarMedicoComHorarioELogarAsync(cliente);
        var (outroMedicoId, _) = await CriarMedicoELogarAsync(cliente);
        var pacienteId = await CriarPacienteAsync();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenMedico);

        var resposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, outroMedicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var listaResposta = await cliente.GetAsync("/api/consultas");
        var consultas = await listaResposta.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        var consulta = Assert.Single(consultas!);
        var medicoIdRetornado = ((JsonElement)consulta["medicoId"]).GetString();
        Assert.Equal(medicoId.ToString(), medicoIdRetornado);
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
    public async Task HorariosLivres_ComoMedico_DeveRetornarOsPropriosSlotsMesmoPassandoOutroMedicoIdNaQuery()
    {
        var cliente = _factory.CreateClient();
        var (_, tokenMedico) = await CriarMedicoComHorarioELogarAsync(cliente);
        var outroMedicoId = await CriarMedicoComHorarioAsync();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenMedico);

        var resposta = await cliente.GetAsync($"/api/consultas/horarios-livres?medicoId={outroMedicoId}&data=2026-07-13");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var slots = await resposta.Content.ReadFromJsonAsync<List<DateTime>>();
        Assert.Equal(6, slots!.Count);
    }

    [Fact]
    public async Task Listar_ComoRecepcao_DeveRetornarTodasAsConsultas()
    {
        var cliente = _factory.CreateClient();
        var tokenRecepcao = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRecepcao);
        var (medicoId1, _) = await CriarMedicoELogarAsync(cliente);
        var (medicoId2, _) = await CriarMedicoELogarAsync(cliente);
        var pacienteId = await CriarPacienteAsync();
        await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId1, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));
        await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId2, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));

        var resposta = await cliente.GetAsync("/api/consultas");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var consultas = await resposta.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.Equal(2, consultas!.Count);
    }

    [Fact]
    public async Task Listar_ComoMedico_DeveRetornarSoAsPropriasConsultasMesmoPassandoOutroMedicoIdNaQuery()
    {
        var cliente = _factory.CreateClient();
        var (medicoId, tokenMedico) = await CriarMedicoELogarAsync(cliente);
        var (outroMedicoId, _) = await CriarMedicoELogarAsync(cliente);
        var pacienteId = await CriarPacienteAsync();

        var tokenRecepcao = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRecepcao);
        await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));
        await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, outroMedicoId, new DateTime(2026, 7, 13, 9, 0, 0, DateTimeKind.Utc), null));

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenMedico);
        var resposta = await cliente.GetAsync($"/api/consultas?medicoId={outroMedicoId}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var consultas = await resposta.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.Single(consultas!);
    }

    [Fact]
    public async Task Listar_ComFiltroDeStatus_DeveRetornarSoAsConsultasComEsseStatus()
    {
        var cliente = _factory.CreateClient();
        var tokenRecepcao = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRecepcao);
        var (medicoId, _) = await CriarMedicoELogarAsync(cliente);
        var pacienteId = await CriarPacienteAsync();
        var criarResposta = await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null));
        var corpo = await criarResposta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        await cliente.PatchAsync($"/api/consultas/{corpo!["id"]}/cancelar", null);
        await cliente.PostAsJsonAsync("/api/consultas",
            new CriarConsultaRequest(pacienteId, medicoId, new DateTime(2026, 7, 13, 9, 0, 0, DateTimeKind.Utc), null));

        var resposta = await cliente.GetAsync("/api/consultas?status=Cancelada");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var consultas = await resposta.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.Single(consultas!);
    }

    [Fact]
    public async Task DiasDisponiveis_ComoRecepcao_DeveRetornarOsDiasComHorarioLivre()
    {
        var cliente = _factory.CreateClient();
        var token = await CriarUsuarioELogarAsync(cliente, PapelUsuario.Recepcao);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var medicoId = await CriarMedicoComHorarioAsync();

        var resposta = await cliente.GetAsync($"/api/consultas/dias-disponiveis?medicoId={medicoId}&ano=2026&mes=7");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var dias = await resposta.Content.ReadFromJsonAsync<List<DateOnly>>();
        Assert.Equal(
            new[] { new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 27) },
            dias);
    }

    [Fact]
    public async Task DiasDisponiveis_ComoMedico_DeveRetornarOsProprioDiasMesmoPassandoOutroMedicoIdNaQuery()
    {
        var cliente = _factory.CreateClient();
        var (_, tokenMedico) = await CriarMedicoComHorarioEmDiaELogarAsync(cliente, DiaSemana.Terca);
        var outroMedicoId = await CriarMedicoComHorarioAsync();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenMedico);

        var resposta = await cliente.GetAsync($"/api/consultas/dias-disponiveis?medicoId={outroMedicoId}&ano=2026&mes=7");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var dias = await resposta.Content.ReadFromJsonAsync<List<DateOnly>>();
        Assert.Equal(
            new[] { new DateOnly(2026, 7, 7), new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 21), new DateOnly(2026, 7, 28) },
            dias);
    }
}
