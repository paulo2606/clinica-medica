using AgendamentoClinica.Api.Utils;
using Xunit;

namespace AgendamentoClinica.Tests.Utils;

public class CpfValidadorTests
{
    [Theory]
    [InlineData("11144477735")]
    [InlineData("529.982.247-25")]
    public void EhValido_ComCpfValido_DeveRetornarTrue(string cpf)
    {
        Assert.True(CpfValidador.EhValido(cpf));
    }

    [Theory]
    [InlineData("11144477736")]
    [InlineData("11111111111")]
    [InlineData("123")]
    [InlineData("")]
    public void EhValido_ComCpfInvalido_DeveRetornarFalse(string cpf)
    {
        Assert.False(CpfValidador.EhValido(cpf));
    }

    [Fact]
    public void Normalizar_DeveManterApenasDigitos()
    {
        Assert.Equal("52998224725", CpfValidador.Normalizar("529.982.247-25"));
    }
}
