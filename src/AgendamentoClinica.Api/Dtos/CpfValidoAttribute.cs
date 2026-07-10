using System.ComponentModel.DataAnnotations;
using AgendamentoClinica.Api.Utils;

namespace AgendamentoClinica.Api.Dtos;

public class CpfValidoAttribute : ValidationAttribute
{
    public CpfValidoAttribute() : base("CPF inválido.") { }

    public override bool IsValid(object? value) =>
        value is string cpf && CpfValidador.EhValido(cpf);
}
