using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record CriarConsultaRequest(
    [Required] Guid PacienteId,
    [Required] Guid MedicoId,
    [Required] DateTime DataHora,
    [MaxLength(500)] string? Observacoes
    );
