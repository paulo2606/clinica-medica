using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IBloqueioAgendaService
{
    Task<(ResultadoOperacao Resultado, Guid? Id)> CriarAsync(
        Guid medicoId, DateTime dataHoraInicio, DateTime dataHoraFim,
        TipoRecorrenciaBloqueio tipoRecorrencia, DateOnly? recorrenciaAte, string? motivo);
    Task<List<BloqueioAgendaMedico>> ListarPorMedicoAsync(Guid medicoId);
    Task<ResultadoOperacao> AtualizarAsync(
        Guid id, Guid? medicoIdRestricao, DateTime dataHoraInicio, DateTime dataHoraFim,
        TipoRecorrenciaBloqueio tipoRecorrencia, DateOnly? recorrenciaAte, string? motivo);
    Task<ResultadoOperacao> RemoverAsync(Guid id, Guid? medicoIdRestricao);
    Task<bool> EstaBloqueadoAsync(Guid medicoId, DateTime periodoInicio, DateTime periodoFim);
}
