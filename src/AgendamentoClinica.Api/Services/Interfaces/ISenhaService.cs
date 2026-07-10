namespace AgendamentoClinica.Api.Services;

public interface ISenhaService
{
    string GerarHash(string senha);
    bool Verificar(string senha, string hash);
}
