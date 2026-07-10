using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record CriarMedicoRequest(
    [Required, MaxLength(200)] string Nome,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [Required, MaxLength(20)] string Telefone,
    [Required, MaxLength(20)] string Crm,
    [Required] Guid EspecialidadeId
    );
