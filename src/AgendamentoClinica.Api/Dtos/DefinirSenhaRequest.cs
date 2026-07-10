using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record DefinirSenhaRequest(
    [Required] string Token,
    [Required, MinLength(8), MaxLength(72)] string NovaSenha
    );
