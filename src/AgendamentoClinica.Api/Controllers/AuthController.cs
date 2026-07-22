using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Services;
using AgendamentoClinica.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string NomeCookieRefresh = "refreshToken";
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [EnableRateLimiting("auth-sensivel")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest requisicao)
    {
        var resultado = await _authService.AutenticarAsync(requisicao.Email, requisicao.Senha);
        if (resultado is null)
        {
            return Unauthorized(new { mensagem = "Credenciais inválidas." });
        }

        DefinirCookieRefresh(resultado.RefreshToken);
        return Ok(new { accessToken = resultado.AccessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(NomeCookieRefresh, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { mensagem = "Sessão inválida." });
        }

        var resultado = await _authService.RenovarAsync(refreshToken);
        if (resultado is null)
        {
            return Unauthorized(new { mensagem = "Sessão inválida ou expirada." });
        }

        DefinirCookieRefresh(resultado.RefreshToken);
        return Ok(new { accessToken = resultado.AccessToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(NomeCookieRefresh, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevogarAsync(refreshToken);
        }

        Response.Cookies.Delete(NomeCookieRefresh);
        return NoContent();
    }

    [Authorize]
    [HttpGet("eu")]
    public IActionResult Eu()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var nome = User.FindFirstValue(ClaimTypes.Name);
        var papel = User.FindFirstValue(ClaimTypes.Role);
        return Ok(new { id, nome, papel });
    }

    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("auth-sensivel")]
    [HttpPost("cadastro")]
    public async Task<IActionResult> Cadastro([FromBody] CriarUsuarioRequest requisicao)
    {
        var id = await _authService.CadastrarAsync(requisicao.Nome, requisicao.Email, requisicao.Telefone, requisicao.Papel);
        if (id is null)
        {
            return BadRequest(new { mensagem = "Não foi possível cadastrar: e-mail ou telefone já cadastrado." });
        }

        return Created(string.Empty, new { id });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios()
    {
        var usuarios = await _authService.ListarUsuariosAsync();
        return Ok(usuarios);
    }

    [Authorize]
    [HttpGet("meu-perfil")]
    public async Task<IActionResult> ObterMeuPerfil()
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var perfil = await _authService.ObterMeuPerfilAsync(usuarioId);
        return perfil is null ? NotFound() : Ok(perfil);
    }

    [Authorize]
    [RequestSizeLimit(ValidadorImagem.TamanhoMaximoBytes)]
    [HttpPut("meu-perfil/foto")]
    public async Task<IActionResult> AtualizarMinhaFoto(IFormFile foto)
    {
        if (foto is null || !ValidadorImagem.TamanhoValido(foto.Length))
        {
            return BadRequest(new { mensagem = "Envie uma imagem JPEG ou PNG de até 3MB." });
        }

        using var memoria = new MemoryStream();
        await foto.CopyToAsync(memoria);
        var conteudo = memoria.ToArray();

        var extensao = ValidadorImagem.DetectarExtensao(conteudo);
        if (extensao is null)
        {
            return BadRequest(new { mensagem = "Formato inválido. Envie uma imagem JPEG ou PNG." });
        }

        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await _authService.AtualizarMinhaFotoAsync(usuarioId, conteudo, extensao);

        var perfil = await _authService.ObterMeuPerfilAsync(usuarioId);
        return Ok(new { fotoUrl = perfil?.FotoUrl });
    }

    [EnableRateLimiting("auth-sensivel")]
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciSenha([FromBody] EsqueciSenhaRequest requisicao)
    {
        await _authService.EsqueciSenhaAsync(requisicao.Email);
        return Ok(new { mensagem = "Se o e-mail informado estiver cadastrado, você receberá instruções para redefinir a senha." });
    }

    [EnableRateLimiting("auth-sensivel")]
    [HttpPost("definir-senha")]
    public async Task<IActionResult> DefinirSenha([FromBody] DefinirSenhaRequest requisicao)
    {
        var resultado = await _authService.DefinirSenhaAsync(requisicao.Token, requisicao.NovaSenha);
        if (resultado != ResultadoOperacao.Sucesso)
        {
            return BadRequest(new { mensagem = "Link inválido, expirado ou já utilizado." });
        }

        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("auth-sensivel")]
    [HttpPut("senha")]
    public async Task<IActionResult> TrocarSenha([FromBody] TrocarSenhaRequest requisicao)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var sucesso = await _authService.TrocarSenhaAsync(usuarioId, requisicao.SenhaAtual, requisicao.NovaSenha);
        if (!sucesso)
        {
            return BadRequest(new { mensagem = "Senha atual incorreta." });
        }

        return NoContent();
    }

    private void DefinirCookieRefresh(string refreshToken)
    {
        Response.Cookies.Append(NomeCookieRefresh, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
