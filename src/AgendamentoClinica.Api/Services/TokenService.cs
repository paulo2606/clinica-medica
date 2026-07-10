using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AgendamentoClinica.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace AgendamentoClinica.Api.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuracao;

    public TokenService(IConfiguration configuracao)
    {
        _configuracao = configuracao;
    }

    public string GerarAccessToken(Usuario usuario)
    {
        var chave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuracao["Jwt:SecretKey"]!));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, usuario.Papel.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuracao["Jwt:Issuer"],
            audience: _configuracao["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GerarRefreshTokenBruto()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
