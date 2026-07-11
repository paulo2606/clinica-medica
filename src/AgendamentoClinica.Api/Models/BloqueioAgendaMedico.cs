namespace AgendamentoClinica.Api.Models;

public class BloqueioAgendaMedico
{
    public Guid Id { get; set; }
    public Guid MedicoId { get; set; }
    public DateTime DataHoraInicio { get; set; }
    public DateTime DataHoraFim { get; set; }
    public TipoRecorrenciaBloqueio TipoRecorrencia { get; set; } = TipoRecorrenciaBloqueio.Nenhuma;
    public DateOnly? RecorrenciaAte { get; set; }
    public string? RegraRecorrencia { get; set; }
    public string? Motivo { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Medico? Medico { get; set; }
}
