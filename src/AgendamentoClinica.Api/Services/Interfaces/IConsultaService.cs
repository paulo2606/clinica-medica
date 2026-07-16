using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IConsultaService
{
    Task<List<DateTime>> CalcularHorariosLivresAsync(Guid medicoId, DateOnly data);
    Task<List<DateOnly>> CalcularDiasDisponiveisAsync(Guid medicoId, int ano, int mes);
    Task<(ResultadoOperacao Resultado, Guid? Id)> CriarAsync(
        Guid pacienteId, Guid medicoId, DateTime dataHora, string? observacoes, Guid criadoPorUsuarioId, TipoConsulta tipo = TipoConsulta.Retorno);
    Task<ResultadoOperacao> CancelarAsync(Guid id);
    Task<ResultadoOperacao> ConfirmarAsync(Guid id);
    Task<ResultadoOperacao> ReagendarAsync(Guid id, DateTime novaDataHora);
    Task<List<Consulta>> ListarAsync(Guid? medicoId, DateOnly? data, StatusConsulta? status);
    Task<Consulta?> ObterPorLembreteMessageSidAsync(string messageSid);
}
