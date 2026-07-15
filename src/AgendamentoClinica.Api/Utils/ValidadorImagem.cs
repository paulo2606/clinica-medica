namespace AgendamentoClinica.Api.Utils;

public static class ValidadorImagem
{
    public const int TamanhoMaximoBytes = 3 * 1024 * 1024;

    public static string? DetectarExtensao(byte[] conteudo)
    {
        if (conteudo.Length >= 3 && conteudo[0] == 0xFF && conteudo[1] == 0xD8 && conteudo[2] == 0xFF)
        {
            return "jpg";
        }

        if (conteudo.Length >= 8 &&
            conteudo[0] == 0x89 && conteudo[1] == 0x50 && conteudo[2] == 0x4E && conteudo[3] == 0x47 &&
            conteudo[4] == 0x0D && conteudo[5] == 0x0A && conteudo[6] == 0x1A && conteudo[7] == 0x0A)
        {
            return "png";
        }

        return null;
    }

    public static bool TamanhoValido(long tamanhoBytes) => tamanhoBytes is > 0 and <= TamanhoMaximoBytes;
}
