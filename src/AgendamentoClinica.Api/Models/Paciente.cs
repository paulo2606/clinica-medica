namespace AgendamentoClinica.Api.Models;

public class Paciente
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly DataNascimento { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
