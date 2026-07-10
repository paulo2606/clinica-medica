using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record EsqueciSenhaRequest(
    [Required, EmailAddress] string Email
    );
