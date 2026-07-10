using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record TrocarSenhaRequest(
    [Required] string SenhaAtual,
    [Required, MinLength(8), MaxLength(72)] string NovaSenha
    );
