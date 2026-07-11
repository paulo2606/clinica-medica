using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class HorarioTrabalhoService : IHorarioTrabalhoService
{
    private readonly AgendamentoDbContext _db;

    public HorarioTrabalhoService(AgendamentoDbContext db)
    {
        _db = db;
    }

    public async Task<(ResultadoOperacao Resultado, List<Guid> Ids)> CriarAsync(
        Guid medicoId, List<DiaSemana> diasSemana, TimeOnly horaInicio, TimeOnly horaFim)
    {
        if (!await _db.Medicos.AnyAsync(m => m.Id == medicoId && m.Ativo))
        {
            return (ResultadoOperacao.NaoEncontrado, []);
        }

        foreach (var diaSemana in diasSemana.Distinct())
        {
            if (await ExisteSobreposicaoAsync(medicoId, diaSemana, horaInicio, horaFim))
            {
                return (ResultadoOperacao.Duplicado, []);
            }
        }

        var horarios = diasSemana.Distinct().Select(diaSemana => new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            DiaSemana = diaSemana,
            HoraInicio = horaInicio,
            HoraFim = horaFim
        }).ToList();
        _db.HorariosTrabalhoMedico.AddRange(horarios);
        await _db.SaveChangesAsync();

        return (ResultadoOperacao.Sucesso, horarios.Select(h => h.Id).ToList());
    }

    public async Task<List<HorarioTrabalhoMedico>> ListarPorMedicoAsync(Guid medicoId) =>
        await _db.HorariosTrabalhoMedico
            .Where(h => h.MedicoId == medicoId)
            .OrderBy(h => h.DiaSemana).ThenBy(h => h.HoraInicio)
            .ToListAsync();

    public async Task<ResultadoOperacao> AtualizarAsync(Guid id, DiaSemana diaSemana, TimeOnly horaInicio, TimeOnly horaFim)
    {
        var horario = await _db.HorariosTrabalhoMedico.FirstOrDefaultAsync(h => h.Id == id);
        if (horario is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        if (await ExisteSobreposicaoAsync(horario.MedicoId, diaSemana, horaInicio, horaFim, exceto: id))
        {
            return ResultadoOperacao.Duplicado;
        }

        horario.DiaSemana = diaSemana;
        horario.HoraInicio = horaInicio;
        horario.HoraFim = horaFim;
        await _db.SaveChangesAsync();

        return ResultadoOperacao.Sucesso;
    }

    public async Task<ResultadoOperacao> RemoverAsync(Guid id)
    {
        var horario = await _db.HorariosTrabalhoMedico.FirstOrDefaultAsync(h => h.Id == id);
        if (horario is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        _db.HorariosTrabalhoMedico.Remove(horario);
        await _db.SaveChangesAsync();

        return ResultadoOperacao.Sucesso;
    }

    private async Task<bool> ExisteSobreposicaoAsync(
        Guid medicoId, DiaSemana diaSemana, TimeOnly horaInicio, TimeOnly horaFim, Guid? exceto = null)
    {
        var horariosNoDia = await _db.HorariosTrabalhoMedico
            .Where(h => h.MedicoId == medicoId && h.DiaSemana == diaSemana && h.Id != exceto)
            .ToListAsync();

        return horariosNoDia.Any(h => horaInicio < h.HoraFim && h.HoraInicio < horaFim);
    }
}
