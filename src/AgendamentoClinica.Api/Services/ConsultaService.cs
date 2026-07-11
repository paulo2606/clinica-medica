using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class ConsultaService : IConsultaService
{
    private const int DuracaoConsultaMinutos = 15;
    private const int BufferMinutos = 5;

    private readonly AgendamentoDbContext _db;
    private readonly IBloqueioAgendaService _bloqueioAgendaService;

    public ConsultaService(AgendamentoDbContext db, IBloqueioAgendaService bloqueioAgendaService)
    {
        _db = db;
        _bloqueioAgendaService = bloqueioAgendaService;
    }

    public async Task<List<DateTime>> CalcularHorariosLivresAsync(Guid medicoId, DateOnly data)
    {
        var horarios = await _db.HorariosTrabalhoMedico
            .Where(h => h.MedicoId == medicoId && h.DiaSemana == data.DayOfWeek)
            .ToListAsync();
        if (horarios.Count == 0)
        {
            return [];
        }

        var inicioDia = data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var fimDia = inicioDia.AddDays(1);
        var consultasDoDia = await _db.Consultas
            .Where(c => c.MedicoId == medicoId && c.Status != StatusConsulta.Cancelada
                && c.DataHora >= inicioDia && c.DataHora < fimDia)
            .ToListAsync();
        var bloqueios = await _bloqueioAgendaService.ListarPorMedicoAsync(medicoId);

        var duracaoConsulta = TimeSpan.FromMinutes(DuracaoConsultaMinutos);
        var duracaoIntervalo = TimeSpan.FromMinutes(DuracaoConsultaMinutos + BufferMinutos);
        var slotsLivres = new List<DateTime>();

        foreach (var horario in horarios)
        {
            var candidato = data.ToDateTime(horario.HoraInicio, DateTimeKind.Utc);
            var fimJanela = data.ToDateTime(horario.HoraFim, DateTimeKind.Utc);

            while (candidato + duracaoConsulta <= fimJanela)
            {
                var fimCandidato = candidato + duracaoConsulta;
                var ocupado = consultasDoDia.Any(c =>
                    candidato < c.DataHora.AddMinutes(c.DuracaoMinutos) && c.DataHora < fimCandidato);
                var bloqueado = bloqueios.Any(b =>
                    RecorrenciaBloqueio.OcorreEm(b.DataHoraInicio, b.DataHoraFim, b.RegraRecorrencia, candidato, fimCandidato));

                if (!ocupado && !bloqueado)
                {
                    slotsLivres.Add(candidato);
                }

                candidato += duracaoIntervalo;
            }
        }

        return slotsLivres.OrderBy(s => s).ToList();
    }
}
