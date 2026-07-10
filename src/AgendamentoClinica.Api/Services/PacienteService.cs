using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class PacienteService : IPacienteService
{
    private readonly AgendamentoDbContext _db;

    public PacienteService(AgendamentoDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> CriarAsync(string nome, string cpf, string telefone, string? email, DateOnly dataNascimento)
    {
        var cpfNormalizado = CpfValidador.Normalizar(cpf);
        if (await _db.Pacientes.AnyAsync(p => p.Cpf == cpfNormalizado))
        {
            return null;
        }

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Cpf = cpfNormalizado,
            Telefone = telefone,
            Email = email,
            DataNascimento = dataNascimento
        };
        _db.Pacientes.Add(paciente);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _db.Pacientes.AnyAsync(p => p.Cpf == cpfNormalizado && p.Id != paciente.Id))
            {
                return null;
            }
            throw;
        }

        return paciente.Id;
    }

    public async Task<List<Paciente>> BuscarAsync(string? cpf, string? nome, bool incluirInativos)
    {
        var consulta = _db.Pacientes.AsQueryable();
        if (!incluirInativos)
        {
            consulta = consulta.Where(p => p.Ativo);
        }
        if (!string.IsNullOrWhiteSpace(cpf))
        {
            var cpfNormalizado = CpfValidador.Normalizar(cpf);
            consulta = consulta.Where(p => p.Cpf == cpfNormalizado);
        }
        if (!string.IsNullOrWhiteSpace(nome))
        {
            var nomeBusca = nome.ToLower();
            consulta = consulta.Where(p => p.Nome.ToLower().Contains(nomeBusca));
        }
        return await consulta.OrderBy(p => p.Nome).ToListAsync();
    }

    public Task<Paciente?> ObterAsync(Guid id) =>
        _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<ResultadoOperacao> AtualizarAsync(Guid id, string nome, string cpf, string telefone, string? email, DateOnly dataNascimento)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id);
        if (paciente is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        var cpfNormalizado = CpfValidador.Normalizar(cpf);
        if (await _db.Pacientes.AnyAsync(p => p.Cpf == cpfNormalizado && p.Id != id))
        {
            return ResultadoOperacao.Duplicado;
        }

        paciente.Nome = nome;
        paciente.Cpf = cpfNormalizado;
        paciente.Telefone = telefone;
        paciente.Email = email;
        paciente.DataNascimento = dataNascimento;
        await _db.SaveChangesAsync();
        return ResultadoOperacao.Sucesso;
    }

    public async Task<ResultadoOperacao> DesativarAsync(Guid id)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == id);
        if (paciente is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        paciente.Ativo = false;
        await _db.SaveChangesAsync();
        return ResultadoOperacao.Sucesso;
    }
}
