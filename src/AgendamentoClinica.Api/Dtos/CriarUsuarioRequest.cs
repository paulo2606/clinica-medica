using System.ComponentModel.DataAnnotations;
using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Dtos;

public record CriarUsuarioRequest(
    [Required, MaxLength(200)] string Nome,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [Required, MinLength(8), MaxLength(72)] string Senha,
    [Required, MaxLength(20)] string Telefone,
    [EnumDataType(typeof(PapelUsuario))] PapelUsuario Papel
    );
