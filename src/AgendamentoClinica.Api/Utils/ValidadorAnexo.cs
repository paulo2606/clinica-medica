namespace AgendamentoClinica.Api.Utils;

public static class ValidadorAnexo
{
    public const int TamanhoMaximoBytes = 5 * 1024 * 1024;
    public const int QuantidadeMaximaPorConsulta = 3;

    private static readonly byte[] AssinaturaPdf = [0x25, 0x50, 0x44, 0x46, 0x2D];

    public static string? DetectarExtensao(byte[] conteudo)
    {
        var extensaoImagem = ValidadorImagem.DetectarExtensao(conteudo);
        if (extensaoImagem is not null)
        {
            return extensaoImagem;
        }

        if (conteudo.Length >= AssinaturaPdf.Length && conteudo.Take(AssinaturaPdf.Length).SequenceEqual(AssinaturaPdf))
        {
            return "pdf";
        }

        return null;
    }

    public static bool TamanhoValido(long tamanhoBytes) => tamanhoBytes is > 0 and <= TamanhoMaximoBytes;
}
