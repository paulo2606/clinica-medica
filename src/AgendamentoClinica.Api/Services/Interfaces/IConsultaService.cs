namespace AgendamentoClinica.Api.Services;

public interface IConsultaService
{
    Task<List<DateTime>> CalcularHorariosLivresAsync(Guid medicoId, DateOnly data);
}
