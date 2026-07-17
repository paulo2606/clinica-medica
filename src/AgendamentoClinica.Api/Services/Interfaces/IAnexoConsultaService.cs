using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IAnexoConsultaService
{
    Task<(ResultadoOperacao Resultado, Guid? Id)> AdicionarAsync(
        Guid consultaId, Guid? medicoIdRestricao, Guid enviadoPorUsuarioId, byte[] conteudo, string extensao, string nomeOriginal);
    Task<(ResultadoOperacao Resultado, List<AnexoConsulta> Anexos)> ListarAsync(Guid consultaId, Guid? medicoIdRestricao);
    Task<(ResultadoOperacao Resultado, byte[]? Conteudo, AnexoConsulta? Anexo)> ObterConteudoAsync(
        Guid consultaId, Guid anexoId, Guid? medicoIdRestricao);
    Task<ResultadoOperacao> RemoverAsync(Guid consultaId, Guid anexoId, Guid? medicoIdRestricao);
}
