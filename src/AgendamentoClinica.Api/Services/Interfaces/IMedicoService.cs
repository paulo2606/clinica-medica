using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IMedicoService
{
    Task<(ResultadoOperacao Resultado, Guid? MedicoId)> CriarAsync(
        string nome, string email, string telefone, string crm, Guid especialidadeId);
    Task<List<Medico>> ListarAsync(bool incluirInativos);
    Task<Medico?> ObterAsync(Guid id);
    Task<ResultadoOperacao> AtualizarAsync(Guid id, string crm, Guid especialidadeId);
    Task<ResultadoOperacao> DesativarAsync(Guid id);
}
