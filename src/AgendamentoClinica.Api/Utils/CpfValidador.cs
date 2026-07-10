namespace AgendamentoClinica.Api.Utils;

public static class CpfValidador
{
    public static string Normalizar(string cpf) => new(cpf.Where(char.IsDigit).ToArray());

    public static bool EhValido(string cpf)
    {
        var digitos = Normalizar(cpf);
        if (digitos.Length != 11 || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        return digitos[9] == CalcularDigitoVerificador(digitos, 9)
            && digitos[10] == CalcularDigitoVerificador(digitos, 10);
    }

    private static char CalcularDigitoVerificador(string digitos, int quantidade)
    {
        var soma = 0;
        var peso = quantidade + 1;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (digitos[i] - '0') * peso--;
        }

        var resto = soma % 11;
        var digitoVerificador = resto < 2 ? 0 : 11 - resto;
        return (char)('0' + digitoVerificador);
    }
}
