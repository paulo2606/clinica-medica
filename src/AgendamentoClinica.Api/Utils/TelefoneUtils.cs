namespace AgendamentoClinica.Api.Utils;

public static class TelefoneUtils
{
    public static string NormalizarParaE164(string telefone)
    {
        var digitos = new string(telefone.Where(char.IsDigit).ToArray());

        if (digitos.Length is 10 or 11)
        {
            digitos = "55" + digitos;
        }

        if (digitos.Length is not (12 or 13) || !digitos.StartsWith("55"))
        {
            throw new FormatException($"Telefone '{telefone}' não pôde ser normalizado para o formato E.164.");
        }

        return "+" + digitos;
    }
}
