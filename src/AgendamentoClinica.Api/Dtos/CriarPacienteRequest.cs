using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record CriarPacienteRequest(
    [Required, MaxLength(200)] string Nome,
    [Required, CpfValido] string Cpf,
    [Required, MaxLength(20)] string Telefone,
    [EmailAddress, MaxLength(200)] string? Email,
    [Required] DateOnly DataNascimento
    );
