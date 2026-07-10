using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
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
        var corpo = MontarCorpoEmailConvite(nomeMedico, link);
        _filaEmail.Enfileirar(new EmailMensagem(email, "Bem-vindo(a) ao Sistema de Agendamento", corpo));
    }

    private static string MontarCorpoEmailConvite(string nomeMedico, string link) => $"""
        <!DOCTYPE html>
        <html lang="pt-BR"><head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Bem-vindo(a) — Clínica+Saúde</title>
        </head>
        <body style="margin:0;">
        <div style="width:600px;margin:0 auto;background:#f6f9f7;font-family:'Segoe UI',Helvetica,Arial,sans-serif;color:#282c29;">

          <div style="padding:36px 40px 28px 40px;text-align:center;">
            <div style="display:inline-flex;align-items:center;gap:10px;">
              <div style="width:34px;height:34px;border-radius:10px;background:#2f7a56;display:flex;align-items:center;justify-content:center;flex-shrink:0;">
                <div style="position:relative;width:16px;height:16px;">
                  <div style="position:absolute;top:7px;left:0;width:16px;height:2px;background:white;border-radius:1px;"></div>
                  <div style="position:absolute;top:0;left:7px;width:2px;height:16px;background:white;border-radius:1px;"></div>
                </div>
              </div>
              <span style="font-size:20px;font-weight:700;letter-spacing:-0.01em;color:#33453c;">Clínica<span style="color:#2f7a56;">+Saúde</span></span>
            </div>
          </div>

          <div style="background:white;border-radius:20px;margin:0 20px;box-shadow:0 1px 3px rgba(0,0,0,0.06);overflow:hidden;">
            <div style="height:6px;background:#2f7a56;"></div>
            <div style="padding:48px 44px 40px 44px;">
              <p style="margin:0 0 22px 0;font-size:15px;font-weight:600;letter-spacing:0.06em;text-transform:uppercase;color:#2f7a56;">Bem-vindo(a)</p>
              <h1 style="margin:0 0 20px 0;font-size:28px;line-height:1.3;font-weight:700;letter-spacing:-0.01em;color:#232622;">Olá, <span>Dr(a). {nomeMedico}</span></h1>
              <p style="margin:0 0 16px 0;font-size:16px;line-height:1.6;color:#565b57;">Cadastro feito com sucesso! Você agora faz parte da equipe médica da Clínica+Saúde.</p>
              <p style="margin:0 0 34px 0;font-size:16px;line-height:1.6;color:#565b57;">Antes de acessar, é necessário redefinir a senha padrão gerada pela plataforma.</p>
              <div style="text-align:center;margin:0 0 30px 0;">
                <a href="{link}" style="display:inline-block;background:#2f7a56;color:white;font-size:16px;font-weight:600;text-decoration:none;padding:16px 40px;border-radius:12px;letter-spacing:0.01em;">Redefinir Senha</a>
              </div>
              <div style="display:flex;align-items:center;gap:10px;justify-content:center;padding:14px 18px;background:#fbeae6;border-radius:12px;">
                <div style="width:8px;height:8px;border-radius:50%;background:#dd5a3a;flex-shrink:0;"></div>
                <p style="margin:0;font-size:14px;font-weight:600;color:#b8492e;">Este link é válido por 48 horas e pode ser usado apenas uma vez.</p>
              </div>
            </div>
          </div>

          <div style="padding:32px 40px 44px 40px;text-align:center;">
            <p style="margin:0 0 6px 0;font-size:13px;color:#8b8f8b;">Se você não esperava este e-mail, pode ignorá-lo com segurança.</p>
            <p style="margin:0;font-size:13px;color:#9a9d9a;">Clínica+Saúde — Sistema de Agendamento</p>
          </div>

        </div>
        </body></html>
        """;
}
