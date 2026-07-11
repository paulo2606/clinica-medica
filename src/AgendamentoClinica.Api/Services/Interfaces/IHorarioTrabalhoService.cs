using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IHorarioTrabalhoService
{
    Task<(ResultadoOperacao Resultado, Guid? Id)> CriarAsync(Guid medicoId, DayOfWeek diaSemana, TimeOnly horaInicio, TimeOnly horaFim);
    Task<List<HorarioTrabalhoMedico>> ListarPorMedicoAsync(Guid medicoId);
    Task<ResultadoOperacao> AtualizarAsync(Guid id, DayOfWeek diaSemana, TimeOnly horaInicio, TimeOnly horaFim);
    Task<ResultadoOperacao> RemoverAsync(Guid id);
}
