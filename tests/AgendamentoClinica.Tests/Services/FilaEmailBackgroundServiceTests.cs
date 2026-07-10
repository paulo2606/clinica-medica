using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class FilaEmailBackgroundServiceTests
{
    private class EmailServiceFake : IEmailService
    {
        private readonly Queue<bool> _resultados;
        public int Chamadas { get; private set; }

        public EmailServiceFake(params bool[] resultados) => _resultados = new Queue<bool>(resultados);

        public Task<bool> EnviarAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(_resultados.Count > 0 ? _resultados.Dequeue() : true);
        }
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
    public async Task ExecuteAsync_ComSucessoNaPrimeiraTentativa_NaoDeveRetentar()
    {
        var fila = new FilaEmail();
        fila.Enfileirar(new EmailMensagem("bruno@clinica.com", "Assunto", "Corpo"));
        var emailService = new EmailServiceFake(true);
        var servico = new FilaEmailBackgroundService(
            fila, emailService, NullLogger<FilaEmailBackgroundService>.Instance, TimeSpan.FromMilliseconds(10));

        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(() => emailService.Chamadas >= 1);
        await Task.Delay(50);
        await servico.StopAsync(CancellationToken.None);

        Assert.Equal(1, emailService.Chamadas);
    }

    [Fact]
    public async Task ExecuteAsync_ComFalhaNaPrimeira_DeveRetentarUmaVez()
    {
        var fila = new FilaEmail();
        fila.Enfileirar(new EmailMensagem("bruno@clinica.com", "Assunto", "Corpo"));
        var emailService = new EmailServiceFake(false, true);
        var servico = new FilaEmailBackgroundService(
            fila, emailService, NullLogger<FilaEmailBackgroundService>.Instance, TimeSpan.FromMilliseconds(10));

        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(() => emailService.Chamadas >= 2);
        await servico.StopAsync(CancellationToken.None);

        Assert.Equal(2, emailService.Chamadas);
    }

    [Fact]
    public async Task ExecuteAsync_ComFalhaNasDuasTentativas_NaoDeveTentarUmaTerceiraVez()
    {
        var fila = new FilaEmail();
        fila.Enfileirar(new EmailMensagem("bruno@clinica.com", "Assunto", "Corpo"));
        var emailService = new EmailServiceFake(false, false);
        var servico = new FilaEmailBackgroundService(
            fila, emailService, NullLogger<FilaEmailBackgroundService>.Instance, TimeSpan.FromMilliseconds(10));

        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(() => emailService.Chamadas >= 2);
        await Task.Delay(100);
        await servico.StopAsync(CancellationToken.None);

        Assert.Equal(2, emailService.Chamadas);
    }
}
