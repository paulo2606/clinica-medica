using System.ComponentModel.DataAnnotations;
using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Dtos;

public record CriarConsultaRequest(
    [Required] Guid PacienteId,
    [Required] Guid MedicoId,
    [Required] DateTime DataHora,
    [MaxLength(500)] string? Observacoes,
    TipoConsulta Tipo = TipoConsulta.Retorno
    );
