using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public interface ITokenService
{
    string GerarAccessToken(Usuario usuario);
    string GerarRefreshTokenBruto();
}
