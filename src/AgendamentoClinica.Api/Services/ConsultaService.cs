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
        var diaSemana = (DiaSemana)(int)data.DayOfWeek;
        var horarios = await _db.HorariosTrabalhoMedico
            .Where(h => h.MedicoId == medicoId && h.DiaSemana == diaSemana)
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
                    RecorrenciaBloqueio.OcorreEm(b.DataHoraInicio, b.DataHoraFim, b.RegraRecorrencia, candidato - TimeSpan.FromMinutes(BufferMinutos), fimCandidato));

                if (!ocupado && !bloqueado)
                {
                    slotsLivres.Add(candidato);
                }

                candidato += duracaoIntervalo;
            }
        }

        return slotsLivres.OrderBy(s => s).ToList();
    }

    public async Task<(ResultadoOperacao Resultado, Guid? Id)> CriarAsync(
        Guid pacienteId, Guid medicoId, DateTime dataHora, string? observacoes, Guid criadoPorUsuarioId)
    {
        if (!await _db.Pacientes.AnyAsync(p => p.Id == pacienteId && p.Ativo)
            || !await _db.Medicos.AnyAsync(m => m.Id == medicoId && m.Ativo))
        {
            return (ResultadoOperacao.NaoEncontrado, null);
        }

        var fim = dataHora.AddMinutes(DuracaoConsultaMinutos);
        if (!await EstaDisponivelAsync(medicoId, dataHora, fim, consultaIdExcluida: null))
        {
            return (ResultadoOperacao.Duplicado, null);
        }

        var consulta = new Consulta
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId,
            MedicoId = medicoId,
            DataHora = dataHora,
            DuracaoMinutos = DuracaoConsultaMinutos,
            Status = StatusConsulta.Agendada,
            Observacoes = observacoes,
            CriadoPorUsuarioId = criadoPorUsuarioId
        };
        _db.Consultas.Add(consulta);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (ResultadoOperacao.ConflitoConcorrencia, null);
        }

        return (ResultadoOperacao.Sucesso, consulta.Id);
    }

    public async Task<ResultadoOperacao> CancelarAsync(Guid id)
    {
        var consulta = await _db.Consultas.FirstOrDefaultAsync(c => c.Id == id);
        if (consulta is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        if (consulta.Status is StatusConsulta.Cancelada or StatusConsulta.Concluida or StatusConsulta.Faltou)
        {
            return ResultadoOperacao.Sucesso;
        }

        consulta.Status = StatusConsulta.Cancelada;
        consulta.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ResultadoOperacao.Sucesso;
    }

    public async Task<ResultadoOperacao> ReagendarAsync(Guid id, DateTime novaDataHora)
    {
        var consulta = await _db.Consultas.FirstOrDefaultAsync(c => c.Id == id);
        if (consulta is null || consulta.Status == StatusConsulta.Cancelada)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        var fim = novaDataHora.AddMinutes(consulta.DuracaoMinutos);
        if (!await EstaDisponivelAsync(consulta.MedicoId, novaDataHora, fim, consultaIdExcluida: id))
        {
            return ResultadoOperacao.Duplicado;
        }

        consulta.DataHora = novaDataHora;
        consulta.AtualizadoEm = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ResultadoOperacao.ConflitoConcorrencia;
        }

        return ResultadoOperacao.Sucesso;
    }

    public async Task<ResultadoOperacao> ConfirmarAsync(Guid id)
    {
        var consulta = await _db.Consultas.FirstOrDefaultAsync(c => c.Id == id);
        if (consulta is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        if (consulta.Status is StatusConsulta.Cancelada or StatusConsulta.Concluida or StatusConsulta.Faltou)
        {
            return ResultadoOperacao.Sucesso;
        }

        consulta.Status = StatusConsulta.Confirmada;
        consulta.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ResultadoOperacao.Sucesso;
    }

    public Task<Consulta?> ObterPorLembreteMessageSidAsync(string messageSid) =>
        _db.Consultas.FirstOrDefaultAsync(c => c.LembreteMessageSid == messageSid);

    public async Task<List<Consulta>> ListarAsync(Guid? medicoId, DateOnly? data, StatusConsulta? status)
    {
        var query = _db.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Medico!)
                .ThenInclude(m => m.Usuario)
            .AsQueryable();

        if (medicoId.HasValue)
        {
            query = query.Where(c => c.MedicoId == medicoId.Value);
        }

        if (data.HasValue)
        {
            var inicioDia = data.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var fimDia = inicioDia.AddDays(1);
            query = query.Where(c => c.DataHora >= inicioDia && c.DataHora < fimDia);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return await query.OrderBy(c => c.DataHora).ToListAsync();
    }

    private async Task<bool> EstaDisponivelAsync(Guid medicoId, DateTime inicio, DateTime fim, Guid? consultaIdExcluida)
    {
        var diaInicio = inicio.Date;
        var diaFim = diaInicio.AddDays(1);
        var consultasDoDia = await _db.Consultas
            .Where(c => c.MedicoId == medicoId && c.Status != StatusConsulta.Cancelada
                && c.Id != consultaIdExcluida && c.DataHora >= diaInicio && c.DataHora < diaFim)
            .ToListAsync();

        var ocupado = consultasDoDia.Any(c => inicio < c.DataHora.AddMinutes(c.DuracaoMinutos) && c.DataHora < fim);
        if (ocupado)
        {
            return false;
        }

        return !await _bloqueioAgendaService.EstaBloqueadoAsync(medicoId, inicio - TimeSpan.FromMinutes(BufferMinutos), fim);
    }
}
