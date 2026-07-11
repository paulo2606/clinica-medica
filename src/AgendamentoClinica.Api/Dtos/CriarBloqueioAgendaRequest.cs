using System.ComponentModel.DataAnnotations;
using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Dtos;

public record CriarBloqueioAgendaRequest(
    [Required] Guid MedicoId,
    [Required] DateTime DataHoraInicio,
    [Required] DateTime DataHoraFim,
    TipoRecorrenciaBloqueio TipoRecorrencia,
    DateOnly? RecorrenciaAte,
    [MaxLength(200)] string? Motivo
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
