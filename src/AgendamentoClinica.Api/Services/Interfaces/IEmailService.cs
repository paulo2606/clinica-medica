using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IEmailService
{
    Task<bool> EnviarAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default);
}
