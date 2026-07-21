using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class AuthService : IAuthService
{
    private static readonly TimeSpan ValidadeTokenRedefinicaoSenha = TimeSpan.FromHours(1);
    private static readonly TimeSpan ValidadeTokenConvite = TimeSpan.FromHours(48);

    private const string HashFalsoParaTempoConstante = "$2a$11$C6UzMDM.H6dfI/f/IKcEeO7ZZzP0iM5T5RQ7EX8LQ.a1kJ3d8v.CO";

    private readonly AgendamentoDbContext _db;
    private readonly ISenhaService _senhaService;
    private readonly ITokenService _tokenService;
    private readonly IFilaEmail _filaEmail;
    private readonly IConfiguration _configuracao;

    public AuthService(
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

    public async Task<LoginResponse?> AutenticarAsync(string email, string senha)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email && u.Ativo);

        var senhaValida = _senhaService.Verificar(senha, usuario?.SenhaHash ?? HashFalsoParaTempoConstante);

        if (usuario is null || !senhaValida)
        {
            return null;
        }

        return await EmitirTokensAsync(usuario);
    }

    public async Task<LoginResponse?> RenovarAsync(string refreshTokenBruto)
    {
        var hash = _tokenService.HashToken(refreshTokenBruto);
        var registro = await _db.TokensRenovacao
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                && t.RevogadoEm == null
                && t.ExpiraEm > DateTime.UtcNow);

        if (registro?.Usuario is null || !registro.Usuario.Ativo)
        {
            return null;
        }

        registro.RevogadoEm = DateTime.UtcNow;
        return await EmitirTokensAsync(registro.Usuario);
    }

    public async Task RevogarAsync(string refreshTokenBruto)
    {
        var hash = _tokenService.HashToken(refreshTokenBruto);
        var registro = await _db.TokensRenovacao
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevogadoEm == null);

        if (registro is not null)
        {
            registro.RevogadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Guid?> CadastrarAsync(string nome, string email, string telefone, PapelUsuario papel)
    {
        if (await _db.Usuarios.AnyAsync(u => u.Email == email || u.Telefone == telefone))
        {
            return null;
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Email = email,
            Telefone = telefone,
            SenhaHash = _senhaService.GerarHash(_tokenService.GerarRefreshTokenBruto()),
            Papel = papel,
            Ativo = true,
            SenhaDefinida = false
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
        _db.TokensConviteSenha.Add(tokenConvite);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _db.Usuarios.AnyAsync(u => (u.Email == email || u.Telefone == telefone) && u.Id != usuario.Id))
            {
                return null;
            }
            throw;
        }

        EnfileirarEmailConvite(nome, email, tokenBruto);

        return usuario.Id;
    }

    public async Task EsqueciSenhaAsync(string email)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == email && u.Ativo);
        if (usuario is null)
        {
            return;
        }

        var tokensAnteriores = await _db.TokensConviteSenha
            .Where(t => t.UsuarioId == usuario.Id && t.UsadoEm == null)
            .ToListAsync();
        foreach (var tokenAnterior in tokensAnteriores)
        {
            tokenAnterior.UsadoEm = DateTime.UtcNow;
        }

        var tokenBruto = _tokenService.GerarRefreshTokenBruto();
        _db.TokensConviteSenha.Add(new TokenConviteSenha
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = _tokenService.HashToken(tokenBruto),
            ExpiraEm = DateTime.UtcNow.Add(ValidadeTokenRedefinicaoSenha)
        });
        await _db.SaveChangesAsync();

        var urlBase = _configuracao["Frontend:UrlBase"]?.TrimEnd('/') ?? "";
        var link = $"{urlBase}/definir-senha?token={Uri.EscapeDataString(tokenBruto)}";
        var corpo = EmailTemplateHtml.MontarCartaoAcao(
            "Redefinição de senha",
            usuario.Nome,
            "Recebemos uma solicitação para redefinir a senha da sua conta.",
            "Se foi você, clique no botão abaixo para criar uma nova senha.",
            "Redefinir Senha",
            link,
            "Este link é válido por 1 hora e pode ser usado apenas uma vez. Se não foi você, ignore este e-mail.");
        _filaEmail.Enfileirar(new EmailMensagem(usuario.Email, "Redefinição de senha", corpo));
    }

    public async Task<ResultadoOperacao> DefinirSenhaAsync(string tokenBruto, string novaSenha)
    {
        var hash = _tokenService.HashToken(tokenBruto);
        var registro = await _db.TokensConviteSenha
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsadoEm == null && t.ExpiraEm > DateTime.UtcNow);

        if (registro?.Usuario is null)
        {
            return ResultadoOperacao.NaoEncontrado;
        }

        registro.Usuario.SenhaHash = _senhaService.GerarHash(novaSenha);
        registro.Usuario.SenhaDefinida = true;
        registro.UsadoEm = DateTime.UtcNow;
        await RevogarTodosOsRefreshTokensAsync(registro.UsuarioId);
        await _db.SaveChangesAsync();

        return ResultadoOperacao.Sucesso;
    }

    public async Task<List<UsuarioResumo>> ListarUsuariosAsync()
    {
        return await _db.Usuarios
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioResumo(u.Id, u.Nome, u.Email, u.Telefone, u.FotoUrl, u.Papel, u.Ativo, u.SenhaDefinida))
            .ToListAsync();
    }

    public async Task<bool> TrocarSenhaAsync(Guid usuarioId, string senhaAtual, string novaSenha)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario is null || !_senhaService.Verificar(senhaAtual, usuario.SenhaHash))
        {
            return false;
        }

        usuario.SenhaHash = _senhaService.GerarHash(novaSenha);
        await RevogarTodosOsRefreshTokensAsync(usuarioId);
        await _db.SaveChangesAsync();

        return true;
    }

    private void EnfileirarEmailConvite(string nome, string email, string tokenBruto)
    {
        var urlBase = _configuracao["Frontend:UrlBase"]?.TrimEnd('/') ?? "";
        var link = $"{urlBase}/definir-senha?token={Uri.EscapeDataString(tokenBruto)}";
        var corpo = EmailTemplateHtml.MontarCartaoAcao(
            "Bem-vindo(a)",
            nome,
            "Cadastro feito com sucesso! Você agora tem acesso ao Sistema de Agendamento.",
            "Antes de acessar, é necessário definir sua senha de acesso.",
            "Definir Senha",
            link,
            "Este link é válido por 48 horas e pode ser usado apenas uma vez.");
        _filaEmail.Enfileirar(new EmailMensagem(email, "Bem-vindo(a) ao Sistema de Agendamento", corpo));
    }

    private async Task RevogarTodosOsRefreshTokensAsync(Guid usuarioId)
    {
        var tokensAtivos = await _db.TokensRenovacao
            .Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null)
            .ToListAsync();
        foreach (var token in tokensAtivos)
        {
            token.RevogadoEm = DateTime.UtcNow;
        }
    }

    private async Task<LoginResponse> EmitirTokensAsync(Usuario usuario)
    {
        var accessToken = _tokenService.GerarAccessToken(usuario);
        var refreshTokenBruto = _tokenService.GerarRefreshTokenBruto();

        _db.TokensRenovacao.Add(new TokenRenovacao
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = _tokenService.HashToken(refreshTokenBruto),
            ExpiraEm = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        return new LoginResponse(accessToken, refreshTokenBruto);
    }
}
