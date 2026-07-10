using AgendamentoClinica.Api.Services;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class SenhaServiceTests
{
    private readonly SenhaService _servico = new();

    [Fact]
    public void GerarHash_DeveGerarHashDiferenteDaSenhaOriginal()
    {
        var hash = _servico.GerarHash("minhaSenha123");

        Assert.NotEqual("minhaSenha123", hash);
    }

    [Fact]
    public void Verificar_ComSenhaCorreta_DeveRetornarTrue()
    {
        var hash = _servico.GerarHash("minhaSenha123");

        Assert.True(_servico.Verificar("minhaSenha123", hash));
    }

    [Fact]
    public void Verificar_ComSenhaIncorreta_DeveRetornarFalse()
    {
        var hash = _servico.GerarHash("minhaSenha123");

        Assert.False(_servico.Verificar("senhaErrada", hash));
    }
}
