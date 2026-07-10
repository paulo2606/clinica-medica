using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Services;
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
        var id = await _authService.CadastrarAsync(requisicao.Nome, requisicao.Email, requisicao.Senha, requisicao.Telefone, requisicao.Papel);
        if (id is null)
        {
            return BadRequest(new { mensagem = "Não foi possível cadastrar: e-mail já cadastrado ou senha muito curta (mínimo 8 caracteres)." });
        }

        return Created(string.Empty, new { id });
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
