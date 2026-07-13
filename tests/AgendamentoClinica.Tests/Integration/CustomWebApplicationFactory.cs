using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgendamentoClinica.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ConexaoTeste =
        "Host=localhost;Port=5432;Database=agendamento_test;Username=postgres;Password=1234";
    public const string ChaveTwilioTeste = "chave-de-teste-twilio";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConexaoTeste,
                ["Jwt:SecretKey"] = "chave-de-teste-com-pelo-menos-32-bytes-1234",
                ["Jwt:Issuer"] = "AgendamentoClinica",
                ["Jwt:Audience"] = "AgendamentoClinica.Cliente",
                ["RateLimiting:AuthSensivel:PermitLimit"] = "1000",
                ["Twilio:AuthToken"] = ChaveTwilioTeste
            });
        });
    }
}
