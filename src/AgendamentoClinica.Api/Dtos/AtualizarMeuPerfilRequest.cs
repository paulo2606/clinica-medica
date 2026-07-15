using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record AtualizarMeuPerfilRequest(
    [Required, MaxLength(200)] string Nome,
    [Required] Guid EspecialidadeId
    );
