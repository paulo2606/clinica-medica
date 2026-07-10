namespace AgendamentoClinica.Api.Models;

public class Consulta
{
    public Guid Id { get; set; }
    public Guid PacienteId { get; set; }
    public Guid MedicoId { get; set; }
    public DateTime DataHora { get; set; }
    public int DuracaoMinutos { get; set; }
    public StatusConsulta Status { get; set; } = StatusConsulta.Agendada;
    public string? Observacoes { get; set; }
    public Guid CriadoPorUsuarioId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public Paciente? Paciente { get; set; }
    public Medico? Medico { get; set; }
    public Usuario? CriadoPorUsuario { get; set; }
}
