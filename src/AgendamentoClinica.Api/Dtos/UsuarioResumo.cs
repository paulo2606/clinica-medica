using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Dtos;

public record UsuarioResumo(
    Guid Id,
    string Nome,
    string Email,
    string Telefone,
    string? FotoUrl,
    PapelUsuario Papel,
    bool Ativo,
    bool SenhaDefinida
    );
