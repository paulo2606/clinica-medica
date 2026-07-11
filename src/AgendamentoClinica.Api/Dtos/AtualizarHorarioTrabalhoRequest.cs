using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record AtualizarHorarioTrabalhoRequest(
    [Required] DayOfWeek DiaSemana,
    [Required] TimeOnly HoraInicio,
    [Required] TimeOnly HoraFim
    ) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HoraInicio >= HoraFim)
        {
            yield return new ValidationResult("HoraInicio deve ser antes de HoraFim.", [nameof(HoraInicio), nameof(HoraFim)]);
        }
    }
}
