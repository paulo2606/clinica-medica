using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class AnexoConsultaService : IAnexoConsultaService
{
    private readonly AgendamentoDbContext _db;
    private readonly IWebHostEnvironment _ambiente;

    public AnexoConsultaService(AgendamentoDbContext db, IWebHostEnvironment ambiente)
    {
        _db = db;
        _ambiente = ambiente;
    }

    public async Task<(ResultadoOperacao Resultado, Guid? Id)> AdicionarAsync(
        Guid consultaId, Guid? medicoIdRestricao, Guid enviadoPorUsuarioId, byte[] conteudo, string extensao, string nomeOriginal)
    {
        var consulta = await _db.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId);
        if (consulta is null || (medicoIdRestricao.HasValue && consulta.MedicoId != medicoIdRestricao.Value))
        {
            return (ResultadoOperacao.NaoEncontrado, null);
        }

        var quantidadeAtual = await _db.AnexosConsulta.CountAsync(a => a.ConsultaId == consultaId);
        if (quantidadeAtual >= ValidadorAnexo.QuantidadeMaximaPorConsulta)
        {
            return (ResultadoOperacao.LimiteExcedido, null);
        }

        var anexo = new AnexoConsulta
        {
            Id = Guid.NewGuid(),
            ConsultaId = consultaId,
            NomeOriginal = nomeOriginal,
            Extensao = extensao,
            TamanhoBytes = conteudo.LongLength,
            EnviadoPorUsuarioId = enviadoPorUsuarioId
        };

        var pasta = Path.Combine(_ambiente.ContentRootPath, "AnexosConsultas", consultaId.ToString());
        Directory.CreateDirectory(pasta);
        await File.WriteAllBytesAsync(Path.Combine(pasta, $"{anexo.Id}.{extensao}"), conteudo);

        _db.AnexosConsulta.Add(anexo);
        await _db.SaveChangesAsync();
        return (ResultadoOperacao.Sucesso, anexo.Id);
    }

    public async Task<(ResultadoOperacao Resultado, List<AnexoConsulta> Anexos)> ListarAsync(Guid consultaId, Guid? medicoIdRestricao)
    {
        var consulta = await _db.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId);
        if (consulta is null || (medicoIdRestricao.HasValue && consulta.MedicoId != medicoIdRestricao.Value))
        {
            return (ResultadoOperacao.NaoEncontrado, []);
        }

        var anexos = await _db.AnexosConsulta
            .Where(a => a.ConsultaId == consultaId)
            .OrderBy(a => a.CriadoEm)
            .ToListAsync();
        return (ResultadoOperacao.Sucesso, anexos);
    }

    public async Task<(ResultadoOperacao Resultado, byte[]? Conteudo, AnexoConsulta? Anexo)> ObterConteudoAsync(
        Guid consultaId, Guid anexoId, Guid? medicoIdRestricao)
    {
        var anexo = await _db.AnexosConsulta
            .Include(a => a.Consulta)
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.ConsultaId == consultaId);
        if (anexo?.Consulta is null || (medicoIdRestricao.HasValue && anexo.Consulta.MedicoId != medicoIdRestricao.Value))
        {
            return (ResultadoOperacao.NaoEncontrado, null, null);
        }

        var caminho = Path.Combine(_ambiente.ContentRootPath, "AnexosConsultas", consultaId.ToString(), $"{anexo.Id}.{anexo.Extensao}");
        var conteudo = await File.ReadAllBytesAsync(caminho);
        return (ResultadoOperacao.Sucesso, conteudo, anexo);
    }

    public async Task<ResultadoOperacao> RemoverAsync(Guid consultaId, Guid anexoId, Guid? medicoIdRestricao)
    {
        var anexo = await _db.AnexosConsulta
            .Include(a => a.Consulta)
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.ConsultaId == consultaId);
        if (anexo?.Consulta is null || (medicoIdRestricao.HasValue && anexo.Consulta.MedicoId != medicoIdRestricao.Value))
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        var caminho = Path.Combine(_ambiente.ContentRootPath, "AnexosConsultas", consultaId.ToString(), $"{anexo.Id}.{anexo.Extensao}");
        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }

        _db.AnexosConsulta.Remove(anexo);
        await _db.SaveChangesAsync();
        return ResultadoOperacao.Sucesso;
    }
}
