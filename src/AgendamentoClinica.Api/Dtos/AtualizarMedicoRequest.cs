using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record AtualizarMedicoRequest(
    [Required, MaxLength(20)] string Crm,
    [Required] Guid EspecialidadeId
    );
