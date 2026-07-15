using System.ComponentModel.DataAnnotations;
using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Dtos;

public record AtualizarBloqueioAgendaRequest(
    [Required] DateTime DataHoraInicio,
    [Required] DateTime DataHoraFim,
    TipoRecorrenciaBloqueio TipoRecorrencia,
    DateOnly? RecorrenciaAte,
    [MaxLength(200)] string? Motivo,
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Cor deve ser um hex válido, ex: #1F4D3A.")] string? Cor = null
    ) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DataHoraInicio >= DataHoraFim)
        {
            yield return new ValidationResult("DataHoraInicio deve ser antes de DataHoraFim.", [nameof(DataHoraInicio), nameof(DataHoraFim)]);
        }
    }
}
