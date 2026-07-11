using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IHorarioTrabalhoService
{
    Task<(ResultadoOperacao Resultado, List<Guid> Ids)> CriarAsync(Guid medicoId, List<DiaSemana> diasSemana, TimeOnly horaInicio, TimeOnly horaFim);
    Task<List<HorarioTrabalhoMedico>> ListarPorMedicoAsync(Guid medicoId);
    Task<ResultadoOperacao> AtualizarAsync(Guid id, DiaSemana diaSemana, TimeOnly horaInicio, TimeOnly horaFim);
    Task<ResultadoOperacao> RemoverAsync(Guid id);
}
