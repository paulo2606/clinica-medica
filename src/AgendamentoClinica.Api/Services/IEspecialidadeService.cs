using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IEspecialidadeService
{
    Task<Guid?> CriarAsync(string nome);
    Task<List<Especialidade>> ListarAsync(bool incluirInativas);
    Task<Especialidade?> ObterAsync(Guid id);
    Task<ResultadoOperacao> AtualizarAsync(Guid id, string nome);
    Task<ResultadoOperacao> DesativarAsync(Guid id);
}
