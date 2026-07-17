using AgendamentoClinica.Api.Utils;
using Xunit;

namespace AgendamentoClinica.Tests.Utils;

public class ValidadorAnexoTests
{
    private static readonly byte[] JpegValido = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] PngValido = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PdfValido = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
    private static readonly byte[] Invalido = [0x00, 0x01, 0x02, 0x03];

    [Fact]
    public void DetectarExtensao_ComJpegValido_DeveRetornarJpg()
    {
        Assert.Equal("jpg", ValidadorAnexo.DetectarExtensao(JpegValido));
    }

    [Fact]
    public void DetectarExtensao_ComPngValido_DeveRetornarPng()
    {
        Assert.Equal("png", ValidadorAnexo.DetectarExtensao(PngValido));
    }

    [Fact]
    public void DetectarExtensao_ComPdfValido_DeveRetornarPdf()
    {
        Assert.Equal("pdf", ValidadorAnexo.DetectarExtensao(PdfValido));
    }

    [Fact]
    public void DetectarExtensao_ComConteudoInvalido_DeveRetornarNulo()
    {
        Assert.Null(ValidadorAnexo.DetectarExtensao(Invalido));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5 * 1024 * 1024, true)]
    [InlineData(5 * 1024 * 1024 + 1, false)]
    public void TamanhoValido_DeveRespeitarLimiteDe5MB(long tamanho, bool esperado)
    {
        Assert.Equal(esperado, ValidadorAnexo.TamanhoValido(tamanho));
    }
}
