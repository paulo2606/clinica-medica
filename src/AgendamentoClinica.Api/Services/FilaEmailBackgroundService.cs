namespace AgendamentoClinica.Api.Services;

public class FilaEmailBackgroundService : BackgroundService
{
    private readonly FilaEmail _fila;
    private readonly IEmailService _emailService;
    private readonly ILogger<FilaEmailBackgroundService> _logger;
    private readonly TimeSpan _esperaRetentativa;

    public FilaEmailBackgroundService(
        FilaEmail fila,
        IEmailService emailService,
        ILogger<FilaEmailBackgroundService> logger,
        TimeSpan? esperaRetentativa = null)
    {
        _fila = fila;
        _emailService = emailService;
        _logger = logger;
        _esperaRetentativa = esperaRetentativa ?? TimeSpan.FromMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mensagem in _fila.Leitor.ReadAllAsync(stoppingToken))
        {
            if (await _emailService.EnviarAsync(mensagem, stoppingToken))
            {
                _logger.LogInformation("E-mail enviado para {Destinatario}", mensagem.Para);
                continue;
            }

            await Task.Delay(_esperaRetentativa, stoppingToken);

            if (await _emailService.EnviarAsync(mensagem, stoppingToken))
            {
                _logger.LogInformation("E-mail enviado para {Destinatario} na segunda tentativa", mensagem.Para);
            }
            else
            {
                _logger.LogError("Falha ao enviar e-mail para {Destinatario} após retry", mensagem.Para);
            }
        }
    }
}
