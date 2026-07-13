using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class LembreteConsultaBackgroundServiceTests
{
    private class WhatsAppServiceFake : IWhatsAppService
    {
        public List<string> TelefonesChamados { get; } = [];

        public Task<string> EnviarLembreteConsultaAsync(
            string telefoneDestino, string nomePaciente, string data, string hora, string nomeMedico, string endereco)
        {
            TelefonesChamados.Add(telefoneDestino);
            return Task.FromResult($"SM{Guid.NewGuid():N}");
        }
    }

    private static (IServiceScopeFactory ScopeFactory, WhatsAppServiceFake WhatsApp, DbContextOptions<AgendamentoDbContext> Opcoes) CriarAmbiente()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var whatsApp = new WhatsAppServiceFake();
        var servicos = new ServiceCollection();
        servicos.AddScoped(_ => new AgendamentoDbContext(opcoes));
        servicos.AddSingleton<IWhatsAppService>(whatsApp);
        servicos.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var provedor = servicos.BuildServiceProvider();
        return (provedor.GetRequiredService<IServiceScopeFactory>(), whatsApp, opcoes);
    }

    private static async Task<(Guid MedicoId, Guid PacienteId)> CriarMedicoEPacienteAsync(AgendamentoDbContext db)
    {
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        db.Especialidades.Add(especialidade);
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(), Nome = "Bruno Medico", Email = $"{Guid.NewGuid()}@clinica.com",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}", SenhaHash = "hash", Papel = PapelUsuario.Medico, Ativo = true
        };
        db.Usuarios.Add(usuario);
        var medico = new Medico { Id = Guid.NewGuid(), UsuarioId = usuario.Id, EspecialidadeId = especialidade.Id, Crm = $"CRM{Random.Shared.Next(1000000, 9999999)}" };
        db.Medicos.Add(medico);
        var paciente = new Paciente
        {
            Id = Guid.NewGuid(), Nome = "Ana Paciente", Cpf = $"{Random.Shared.NextInt64(10000000000, 99999999999)}",
            Telefone = "41988887766", DataNascimento = new DateOnly(1990, 1, 1)
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        return (medico.Id, paciente.Id);
    }

    private static async Task AguardarAsync(Func<bool> condicao)
    {
        var tentativas = 0;
        while (!condicao() && tentativas < 200)
        {
            await Task.Delay(10);
            tentativas++;
        }
    }

    [Fact]
    public async Task ExecuteAsync_ComConsultaDentroDaJanelaDe24h_DeveEnviarEMarcarLembreteEnviado()
    {
        var (scopeFactory, whatsApp, opcoes) = CriarAmbiente();
        await using var db = new AgendamentoDbContext(opcoes);
        var (medicoId, pacienteId) = await CriarMedicoEPacienteAsync(db);
        var consulta = new Consulta
        {
            Id = Guid.NewGuid(), MedicoId = medicoId, PacienteId = pacienteId,
            DataHora = DateTime.UtcNow.AddHours(20), DuracaoMinutos = 15,
            Status = StatusConsulta.Agendada, CriadoPorUsuarioId = Guid.NewGuid()
        };
        db.Consultas.Add(consulta);
        await db.SaveChangesAsync();

        var servico = new LembreteConsultaBackgroundService(
            scopeFactory, NullLogger<LembreteConsultaBackgroundService>.Instance, TimeSpan.FromMilliseconds(10));
        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(() => whatsApp.TelefonesChamados.Count >= 1);
        await servico.StopAsync(CancellationToken.None);

        Assert.Single(whatsApp.TelefonesChamados);
        Assert.Equal("+5541988887766", whatsApp.TelefonesChamados[0]);
        await using var dbVerificacao = new AgendamentoDbContext(opcoes);
        var consultaAtualizada = await dbVerificacao.Consultas.FindAsync(consulta.Id);
        Assert.NotNull(consultaAtualizada!.LembreteEnviadoEm);
        Assert.NotNull(consultaAtualizada.LembreteMessageSid);
    }

    [Fact]
    public async Task ExecuteAsync_ComConsultaForaDaJanela_NaoDeveEnviar()
    {
        var (scopeFactory, whatsApp, opcoes) = CriarAmbiente();
        await using var db = new AgendamentoDbContext(opcoes);
        var (medicoId, pacienteId) = await CriarMedicoEPacienteAsync(db);
        db.Consultas.Add(new Consulta
        {
            Id = Guid.NewGuid(), MedicoId = medicoId, PacienteId = pacienteId,
            DataHora = DateTime.UtcNow.AddHours(48), DuracaoMinutos = 15,
            Status = StatusConsulta.Agendada, CriadoPorUsuarioId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var servico = new LembreteConsultaBackgroundService(
            scopeFactory, NullLogger<LembreteConsultaBackgroundService>.Instance, TimeSpan.FromMilliseconds(10));
        await servico.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await servico.StopAsync(CancellationToken.None);

        Assert.Empty(whatsApp.TelefonesChamados);
    }

    [Fact]
    public async Task ExecuteAsync_ComLembreteJaEnviado_NaoDeveReenviar()
    {
        var (scopeFactory, whatsApp, opcoes) = CriarAmbiente();
        await using var db = new AgendamentoDbContext(opcoes);
        var (medicoId, pacienteId) = await CriarMedicoEPacienteAsync(db);
        db.Consultas.Add(new Consulta
        {
            Id = Guid.NewGuid(), MedicoId = medicoId, PacienteId = pacienteId,
            DataHora = DateTime.UtcNow.AddHours(20), DuracaoMinutos = 15,
            Status = StatusConsulta.Agendada, CriadoPorUsuarioId = Guid.NewGuid(),
            LembreteEnviadoEm = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var servico = new LembreteConsultaBackgroundService(
            scopeFactory, NullLogger<LembreteConsultaBackgroundService>.Instance, TimeSpan.FromMilliseconds(10));
        await servico.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await servico.StopAsync(CancellationToken.None);

        Assert.Empty(whatsApp.TelefonesChamados);
    }

    [Fact]
    public async Task ExecuteAsync_ComConsultaCancelada_NaoDeveEnviar()
    {
        var (scopeFactory, whatsApp, opcoes) = CriarAmbiente();
        await using var db = new AgendamentoDbContext(opcoes);
        var (medicoId, pacienteId) = await CriarMedicoEPacienteAsync(db);
        db.Consultas.Add(new Consulta
        {
            Id = Guid.NewGuid(), MedicoId = medicoId, PacienteId = pacienteId,
            DataHora = DateTime.UtcNow.AddHours(20), DuracaoMinutos = 15,
            Status = StatusConsulta.Cancelada, CriadoPorUsuarioId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var servico = new LembreteConsultaBackgroundService(
            scopeFactory, NullLogger<LembreteConsultaBackgroundService>.Instance, TimeSpan.FromMilliseconds(10));
        await servico.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await servico.StopAsync(CancellationToken.None);

        Assert.Empty(whatsApp.TelefonesChamados);
    }
}
