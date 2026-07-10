using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IPacienteService
{
    Task<Guid?> CriarAsync(string nome, string cpf, string telefone, string? email, DateOnly dataNascimento);
    Task<List<Paciente>> BuscarAsync(string? cpf, string? nome, bool incluirInativos);
    Task<Paciente?> ObterAsync(Guid id);
    Task<ResultadoOperacao> AtualizarAsync(Guid id, string nome, string cpf, string telefone, string? email, DateOnly dataNascimento);
    Task<ResultadoOperacao> DesativarAsync(Guid id);
}
