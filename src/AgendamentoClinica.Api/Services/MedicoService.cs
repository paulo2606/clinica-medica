using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class MedicoService : IMedicoService
{
    private static readonly TimeSpan ValidadeTokenConvite = TimeSpan.FromHours(48);

    private const int DuracaoConsultaPadraoMinutos = 15;

    private readonly AgendamentoDbContext _db;
    private readonly ISenhaService _senhaService;
    private readonly ITokenService _tokenService;
    private readonly IFilaEmail _filaEmail;
    private readonly IConfiguration _configuracao;

    public MedicoService(
        AgendamentoDbContext db,
        ISenhaService senhaService,
        ITokenService tokenService,
        IFilaEmail filaEmail,
        IConfiguration configuracao)
    {
        _db = db;
        _senhaService = senhaService;
        _tokenService = tokenService;
        _filaEmail = filaEmail;
        _configuracao = configuracao;
    }

    public async Task<(ResultadoOperacao Resultado, Guid? MedicoId)> CriarAsync(
        string nome, string email, string telefone, string crm, Guid especialidadeId)
    {
        if (!await _db.Especialidades.AnyAsync(e => e.Id == especialidadeId && e.Ativo))
        {
            return (ResultadoOperacao.NaoEncontrado, null);
        }

        if (await DadosJaCadastradosAsync(email, telefone, crm))
        {
            return (ResultadoOperacao.Duplicado, null);
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Email = email,
            Telefone = telefone,
            SenhaHash = _senhaService.GerarHash(_tokenService.GerarRefreshTokenBruto()),
            Papel = PapelUsuario.Medico,
            Ativo = true
        };
        var medico = new Medico
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            EspecialidadeId = especialidadeId,
            Crm = crm,
            DuracaoConsultaPadraoMinutos = DuracaoConsultaPadraoMinutos
        };
        var tokenBruto = _tokenService.GerarRefreshTokenBruto();
        var tokenConvite = new TokenConviteSenha
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = _tokenService.HashToken(tokenBruto),
            ExpiraEm = DateTime.UtcNow.Add(ValidadeTokenConvite)
        };

        _db.Usuarios.Add(usuario);
        _db.Medicos.Add(medico);
        _db.TokensConviteSenha.Add(tokenConvite);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await DadosJaCadastradosAsync(email, telefone, crm, exceto: usuario.Id))
            {
                return (ResultadoOperacao.Duplicado, null);
            }
            throw;
        }

        EnfileirarEmailConvite(nome, email, tokenBruto);

        return (ResultadoOperacao.Sucesso, medico.Id);
    }

    public async Task<List<Medico>> ListarAsync(bool incluirInativos)
    {
        var consulta = _db.Medicos.Include(m => m.Usuario).Include(m => m.Especialidade).AsQueryable();
        if (!incluirInativos)
        {
            consulta = consulta.Where(m => m.Ativo);
        }
        return await consulta.OrderBy(m => m.Usuario!.Nome).ToListAsync();
    }

    public Task<Medico?> ObterAsync(Guid id) =>
        _db.Medicos.Include(m => m.Usuario).Include(m => m.Especialidade).FirstOrDefaultAsync(m => m.Id == id);

    public async Task<ResultadoOperacao> AtualizarAsync(Guid id, string crm, Guid especialidadeId)
    {
        var medico = await _db.Medicos.FirstOrDefaultAsync(m => m.Id == id);
        if (medico is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        if (!await _db.Especialidades.AnyAsync(e => e.Id == especialidadeId && e.Ativo))
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        if (await _db.Medicos.AnyAsync(m => m.Crm == crm && m.Id != id))
        {
            return ResultadoOperacao.Duplicado;
        }

        medico.Crm = crm;
        medico.EspecialidadeId = especialidadeId;
        await _db.SaveChangesAsync();
        return ResultadoOperacao.Sucesso;
    }

    public async Task<ResultadoOperacao> DesativarAsync(Guid id)
    {
        var medico = await _db.Medicos.FirstOrDefaultAsync(m => m.Id == id);
        if (medico is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        medico.Ativo = false;
        await _db.SaveChangesAsync();
        return ResultadoOperacao.Sucesso;
    }

    private async Task<bool> DadosJaCadastradosAsync(string email, string telefone, string crm, Guid? exceto = null)
    {
        if (await _db.Usuarios.AnyAsync(u => (u.Email == email || u.Telefone == telefone) && u.Id != exceto))
        {
            return true;
        }
        return await _db.Medicos.AnyAsync(m => m.Crm == crm);
    }

    private void EnfileirarEmailConvite(string nomeMedico, string email, string tokenBruto)
    {
        var urlBase = _configuracao["Frontend:UrlBase"]?.TrimEnd('/') ?? "";
        var link = $"{urlBase}/definir-senha?token={Uri.EscapeDataString(tokenBruto)}";
        var corpo = EmailTemplateHtml.MontarCartaoAcao(
            "Bem-vindo(a)",
            $"Dr(a). {nomeMedico}",
            "Cadastro feito com sucesso! Você agora faz parte da equipe médica da Clínica+Saúde.",
            "Antes de acessar, é necessário redefinir a senha padrão gerada pela plataforma.",
            "Redefinir Senha",
            link,
            "Este link é válido por 48 horas e pode ser usado apenas uma vez.");
        _filaEmail.Enfileirar(new EmailMensagem(email, "Bem-vindo(a) ao Sistema de Agendamento", corpo));
    }
}
