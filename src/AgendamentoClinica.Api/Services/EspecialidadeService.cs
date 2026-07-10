using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class EspecialidadeService : IEspecialidadeService
{
    private readonly AgendamentoDbContext _db;

    public EspecialidadeService(AgendamentoDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> CriarAsync(string nome)
    {
        if (await _db.Especialidades.AnyAsync(e => e.Nome == nome))
        {
            return null;
        }

        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = nome };
        _db.Especialidades.Add(especialidade);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _db.Especialidades.AnyAsync(e => e.Nome == nome && e.Id != especialidade.Id))
            {
                return null;
            }
            throw;
        }

        return especialidade.Id;
    }

    public async Task<List<Especialidade>> ListarAsync(bool incluirInativas)
    {
        var consulta = _db.Especialidades.AsQueryable();
        if (!incluirInativas)
        {
            consulta = consulta.Where(e => e.Ativo);
        }
        return await consulta.OrderBy(e => e.Nome).ToListAsync();
    }

    public Task<Especialidade?> ObterAsync(Guid id) =>
        _db.Especialidades.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<ResultadoOperacao> AtualizarAsync(Guid id, string nome)
    {
        var especialidade = await _db.Especialidades.FirstOrDefaultAsync(e => e.Id == id);
        if (especialidade is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        if (await _db.Especialidades.AnyAsync(e => e.Nome == nome && e.Id != id))
        {
            return ResultadoOperacao.Duplicado;
        }

        especialidade.Nome = nome;
        await _db.SaveChangesAsync();
        return ResultadoOperacao.Sucesso;
    }

    public async Task<ResultadoOperacao> DesativarAsync(Guid id)
    {
        var especialidade = await _db.Especialidades.FirstOrDefaultAsync(e => e.Id == id);
        if (especialidade is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        especialidade.Ativo = false;
        await _db.SaveChangesAsync();
        return ResultadoOperacao.Sucesso;
    }
}
