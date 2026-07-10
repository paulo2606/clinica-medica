using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IFilaEmail
{
    void Enfileirar(EmailMensagem mensagem);
}
