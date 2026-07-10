namespace AgendamentoClinica.Api.Models;

public class HorarioTrabalhoMedico
{
    public Guid Id { get; set; }
    public Guid MedicoId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim { get; set; }

    public Medico? Medico { get; set; }
}
