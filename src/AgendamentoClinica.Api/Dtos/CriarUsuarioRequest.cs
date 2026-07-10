using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Dtos;

public record CriarUsuarioRequest(
    string Nome, 
    string Email, 
    string Senha,
    string Telefone,
    PapelUsuario Papel
    );
