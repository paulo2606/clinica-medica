using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> AutenticarAsync(string email, string senha);
    Task<LoginResponse?> RenovarAsync(string refreshTokenBruto);
    Task RevogarAsync(string refreshTokenBruto);
    Task<Guid?> CadastrarAsync(string nome, string email, string telefone, PapelUsuario papel);
    Task EsqueciSenhaAsync(string email);
    Task<ResultadoOperacao> DefinirSenhaAsync(string tokenBruto, string novaSenha);
    Task<bool> TrocarSenhaAsync(Guid usuarioId, string senhaAtual, string novaSenha);
    Task<List<UsuarioResumo>> ListarUsuariosAsync();
}
