using System.Security.Cryptography;
using System.Text;
using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Services;

public class AuthService : IAuthService
{
    private const int TamanhoMinimoSenha = 8;

    // Hash "de mentira" usado quando o e-mail não existe, só pra forçar o
    // BCrypt a rodar do mesmo jeito — sem isso, login com e-mail inexistente
    // responde mais rápido que login com senha errada, e dá pra enumerar
    // e-mails cadastrados só medindo o tempo de resposta (timing attack).
    private const string HashFalsoParaTempoConstante = "$2a$11$C6UzMDM.H6dfI/f/IKcEeO7ZZzP0iM5T5RQ7EX8LQ.a1kJ3d8v.CO";

    private readonly AgendamentoDbContext _db;
    private readonly ISenhaService _senhaService;
    private readonly ITokenService _tokenService;

    public AuthService(AgendamentoDbContext db, ISenhaService senhaService, ITokenService tokenService)
    {
        _db = db;
        _senhaService = senhaService;
        _tokenService = tokenService;
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
        var hash = HashToken(refreshTokenBruto);
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
        var hash = HashToken(refreshTokenBruto);
        var registro = await _db.TokensRenovacao
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevogadoEm == null);

        if (registro is not null)
        {
            registro.RevogadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Guid?> CadastrarAsync(string nome, string email, string senha, string telefone, PapelUsuario papel)
    {
        if (senha.Length < TamanhoMinimoSenha)
        {
            return null;
        }

        if (await _db.Usuarios.AnyAsync(u => u.Email == email))
        {
            return null;
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Email = email,
            Telefone = telefone,
            SenhaHash = _senhaService.GerarHash(senha),
            Papel = papel,
            Ativo = true
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return usuario.Id;
    }

    private async Task<LoginResponse> EmitirTokensAsync(Usuario usuario)
    {
        var accessToken = _tokenService.GerarAccessToken(usuario);
        var refreshTokenBruto = _tokenService.GerarRefreshTokenBruto();

        _db.TokensRenovacao.Add(new TokenRenovacao
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = HashToken(refreshTokenBruto),
            ExpiraEm = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        return new LoginResponse(accessToken, refreshTokenBruto);
    }

    private static string HashToken(string tokenBruto)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(tokenBruto));
        return Convert.ToHexString(bytes);
    }
}
