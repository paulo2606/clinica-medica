using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class LembreteConsultaBackgroundService : BackgroundService
{
    private static readonly TimeSpan JanelaAntecedencia = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LembreteConsultaBackgroundService> _logger;
    private readonly TimeSpan _intervaloTick;

    public LembreteConsultaBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LembreteConsultaBackgroundService> logger,
        TimeSpan? intervaloTick = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervaloTick = intervaloTick ?? TimeSpan.FromMinutes(15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervaloTick);
        do
        {
            try
            {
                await ProcessarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar lembretes de consulta");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessarAsync(CancellationToken cancellationToken)
    {
        using var escopo = _scopeFactory.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var whatsAppService = escopo.ServiceProvider.GetRequiredService<IWhatsAppService>();
        var configuracao = escopo.ServiceProvider.GetRequiredService<IConfiguration>();
        var endereco = configuracao["Clinica:Endereco"] ?? "";

        var agora = DateTime.UtcNow;
        var limite = agora.Add(JanelaAntecedencia);

        var consultas = await db.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Medico!)
                .ThenInclude(m => m.Usuario)
            .Where(c => c.Status == StatusConsulta.Agendada
                && c.LembreteEnviadoEm == null
                && c.DataHora >= agora && c.DataHora <= limite)
            .ToListAsync(cancellationToken);

        foreach (var consulta in consultas)
        {
            try
            {
                var telefone = TelefoneUtils.NormalizarParaE164(consulta.Paciente!.Telefone);
                var sid = await whatsAppService.EnviarLembreteConsultaAsync(
                    telefone,
                    consulta.Paciente.Nome,
                    consulta.DataHora.ToString("dd/MM/yyyy"),
                    consulta.DataHora.ToString("HH:mm"),
                    consulta.Medico!.Usuario!.Nome,
                    endereco);

                consulta.LembreteEnviadoEm = agora;
                consulta.LembreteMessageSid = sid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar lembrete de WhatsApp pra consulta {ConsultaId}", consulta.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
