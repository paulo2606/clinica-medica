namespace AgendamentoClinica.Api.Services;

public interface IWhatsAppService
{
    Task<string> EnviarLembreteConsultaAsync(
        string telefoneDestino, string nomePaciente, string data, string hora, string nomeMedico, string endereco);
}
