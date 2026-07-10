using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record CriarEspecialidadeRequest(
    [Required, MaxLength(100)] string Nome
    );
