namespace AgendamentoClinica.Api.Models;

public class TokenRenovacao
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Usuario? Usuario { get; set; }
}
