using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class TokenServiceTests
{
    private static TokenService CriarServico()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "chave-de-teste-com-pelo-menos-32-bytes-1234",
                ["Jwt:Issuer"] = "AgendamentoClinica",
                ["Jwt:Audience"] = "AgendamentoClinica.Cliente"
            })
            .Build();

        return new TokenService(configuracao);
    }

    [Fact]
    public void GerarAccessToken_DeveIncluirClaimsDoUsuario()
    {
        var servico = CriarServico();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Ana Recepcionista",
            Email = "ana@clinica.com",
            Papel = PapelUsuario.Recepcao
        };

        var token = servico.GerarAccessToken(usuario);
        var tokenLido = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(usuario.Id.ToString(), tokenLido.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Contains(tokenLido.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Recepcao");
    }

    [Fact]
    public void GerarRefreshTokenBruto_DeveGerarValoresDiferentesACadaChamada()
    {
        var servico = CriarServico();

        var token1 = servico.GerarRefreshTokenBruto();
        var token2 = servico.GerarRefreshTokenBruto();

        Assert.NotEqual(token1, token2);
    }
}
