using AgendamentoClinica.Api.Utils;
using Xunit;

namespace AgendamentoClinica.Tests.Utils;

public class TelefoneUtilsTests
{
    [Theory]
    [InlineData("41988887766", "+5541988887766")]
    [InlineData("4188887766", "+554188887766")]
    [InlineData("+5541988887766", "+5541988887766")]
    [InlineData("5541988887766", "+5541988887766")]
    [InlineData("(41) 98888-7766", "+5541988887766")]
    public void NormalizarParaE164_ComTelefoneValido_DeveRetornarFormatoE164(string telefone, string esperado)
    {
        Assert.Equal(esperado, TelefoneUtils.NormalizarParaE164(telefone));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("419888877661")]
    public void NormalizarParaE164_ComTelefoneInvalido_DeveLancarFormatException(string telefone)
    {
        Assert.Throws<FormatException>(() => TelefoneUtils.NormalizarParaE164(telefone));
    }
}
