using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AgendamentoClinica.Tests.Integration;

public class RateLimitedWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthSensivel:PermitLimit"] = "3"
            });
        });
    }
}
