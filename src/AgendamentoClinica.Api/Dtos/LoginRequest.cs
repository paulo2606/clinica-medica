using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Senha
    );
