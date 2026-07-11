using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class BloqueioAgendaService : IBloqueioAgendaService
{
    private readonly AgendamentoDbContext _db;

    public BloqueioAgendaService(AgendamentoDbContext db)
    {
        _db = db;
    }

    public async Task<(ResultadoOperacao Resultado, Guid? Id)> CriarAsync(
        Guid medicoId, DateTime dataHoraInicio, DateTime dataHoraFim,
        TipoRecorrenciaBloqueio tipoRecorrencia, DateOnly? recorrenciaAte, string? motivo)
    {
        if (!await _db.Medicos.AnyAsync(m => m.Id == medicoId && m.Ativo))
        {
            return (ResultadoOperacao.NaoEncontrado, null);
        }

        var bloqueio = new BloqueioAgendaMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            DataHoraInicio = dataHoraInicio,
            DataHoraFim = dataHoraFim,
            TipoRecorrencia = tipoRecorrencia,
            RecorrenciaAte = recorrenciaAte,
            RegraRecorrencia = RecorrenciaBloqueio.MontarRegra(tipoRecorrencia, recorrenciaAte),
            Motivo = motivo
        };
        _db.BloqueiosAgendaMedico.Add(bloqueio);
        await _db.SaveChangesAsync();

        return (ResultadoOperacao.Sucesso, bloqueio.Id);
    }

    public async Task<List<BloqueioAgendaMedico>> ListarPorMedicoAsync(Guid medicoId) =>
        await _db.BloqueiosAgendaMedico
            .Where(b => b.MedicoId == medicoId)
            .OrderBy(b => b.DataHoraInicio)
            .ToListAsync();

    public async Task<ResultadoOperacao> AtualizarAsync(
        Guid id, Guid? medicoIdRestricao, DateTime dataHoraInicio, DateTime dataHoraFim,
        TipoRecorrenciaBloqueio tipoRecorrencia, DateOnly? recorrenciaAte, string? motivo)
    {
        var bloqueio = await _db.BloqueiosAgendaMedico.FirstOrDefaultAsync(b => b.Id == id);
        if (bloqueio is null || (medicoIdRestricao.HasValue && bloqueio.MedicoId != medicoIdRestricao.Value))
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        bloqueio.DataHoraInicio = dataHoraInicio;
        bloqueio.DataHoraFim = dataHoraFim;
        bloqueio.TipoRecorrencia = tipoRecorrencia;
        bloqueio.RecorrenciaAte = recorrenciaAte;
        bloqueio.RegraRecorrencia = RecorrenciaBloqueio.MontarRegra(tipoRecorrencia, recorrenciaAte);
        bloqueio.Motivo = motivo;
        await _db.SaveChangesAsync();

        return ResultadoOperacao.Sucesso;
    }

    public async Task<ResultadoOperacao> RemoverAsync(Guid id, Guid? medicoIdRestricao)
    {
        var bloqueio = await _db.BloqueiosAgendaMedico.FirstOrDefaultAsync(b => b.Id == id);
        if (bloqueio is null || (medicoIdRestricao.HasValue && bloqueio.MedicoId != medicoIdRestricao.Value))
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        _db.BloqueiosAgendaMedico.Remove(bloqueio);
        await _db.SaveChangesAsync();

        return ResultadoOperacao.Sucesso;
    }

    public async Task<bool> EstaBloqueadoAsync(Guid medicoId, DateTime periodoInicio, DateTime periodoFim)
    {
        var bloqueios = await _db.BloqueiosAgendaMedico
            .Where(b => b.MedicoId == medicoId)
            .ToListAsync();

        return bloqueios.Any(b => RecorrenciaBloqueio.OcorreEm(
            b.DataHoraInicio, b.DataHoraFim, b.RegraRecorrencia, periodoInicio, periodoFim));
    }
}
