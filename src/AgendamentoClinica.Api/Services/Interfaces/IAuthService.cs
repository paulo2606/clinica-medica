using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> AutenticarAsync(string email, string senha);
    Task<LoginResponse?> RenovarAsync(string refreshTokenBruto);
    Task RevogarAsync(string refreshTokenBruto);
    Task<Guid?> CadastrarAsync(string nome, string email, string senha, string telefone, PapelUsuario papel);
}
