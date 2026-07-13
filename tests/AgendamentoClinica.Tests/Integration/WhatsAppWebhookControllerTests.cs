using System.Net;
using System.Security.Cryptography;
using System.Text;
using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AgendamentoClinica.Tests.Integration;

[Collection("BancoDeTeste")]
public class WhatsAppWebhookControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string UrlWebhook = "http://localhost/api/whatsapp/webhook";

    private class WhatsAppServiceFake : IWhatsAppService
    {
        public List<(string Telefone, string Mensagem)> MensagensEnviadas { get; } = [];

        public Task<string> EnviarLembreteConsultaAsync(
            string telefoneDestino, string nomePaciente, string data, string hora, string nomeMedico, string endereco) =>
            Task.FromResult($"SM{Guid.NewGuid():N}");

        public Task EnviarMensagemLivreAsync(string telefoneDestino, string mensagem)
        {
            MensagensEnviadas.Add((telefoneDestino, mensagem));
            return Task.CompletedTask;
        }
    }

    private readonly CustomWebApplicationFactory _factory;
    private readonly WhatsAppServiceFake _whatsApp = new();

    public WhatsAppWebhookControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        await db.Database.MigrateAsync();
        db.Consultas.RemoveRange(db.Consultas);
        db.Pacientes.RemoveRange(db.Pacientes);
        db.Medicos.RemoveRange(db.Medicos);
        db.Especialidades.RemoveRange(db.Especialidades);
        db.Usuarios.RemoveRange(db.Usuarios);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CriarConsultaComLembreteEnviadoAsync(string messageSid)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(), Nome = "Bruno Medico", Email = $"{Guid.NewGuid()}@clinica.com",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}", SenhaHash = "hash", Papel = PapelUsuario.Medico, Ativo = true
        };
        var medico = new Medico { Id = Guid.NewGuid(), UsuarioId = usuario.Id, EspecialidadeId = especialidade.Id, Crm = $"CRM{Random.Shared.Next(1000000, 9999999)}" };
        var paciente = new Paciente
        {
            Id = Guid.NewGuid(), Nome = "Ana Paciente", Cpf = $"{Random.Shared.NextInt64(10000000000, 99999999999)}",
            Telefone = "41988887766", DataNascimento = new DateOnly(1990, 1, 1)
        };
        var consulta = new Consulta
        {
            Id = Guid.NewGuid(), MedicoId = medico.Id, PacienteId = paciente.Id,
            DataHora = DateTime.UtcNow.AddHours(20), DuracaoMinutos = 15,
            Status = StatusConsulta.Agendada, CriadoPorUsuarioId = usuario.Id,
            LembreteEnviadoEm = DateTime.UtcNow, LembreteMessageSid = messageSid
        };
        db.AddRange(especialidade, usuario, medico, paciente, consulta);
        await db.SaveChangesAsync();
        return consulta.Id;
    }

    private static string CalcularAssinatura(string url, IDictionary<string, string> parametros, string authToken)
    {
        var dados = url + string.Concat(parametros.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + p.Value));
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(dados)));
    }

    private async Task<HttpResponseMessage> EnviarWebhookAsync(Dictionary<string, string> parametros, string? assinatura = null)
    {
        var fabrica = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(servicos =>
            {
                servicos.RemoveAll<IWhatsAppService>();
                servicos.AddSingleton<IWhatsAppService>(_whatsApp);
            });
        });
        var cliente = fabrica.CreateClient();
        assinatura ??= CalcularAssinatura(UrlWebhook, parametros, CustomWebApplicationFactory.ChaveTwilioTeste);
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp/webhook")
        {
            Content = new FormUrlEncodedContent(parametros)
        };
        requisicao.Headers.Add("X-Twilio-Signature", assinatura);
        return await cliente.SendAsync(requisicao);
    }

    [Fact]
    public async Task Webhook_ComAssinaturaInvalida_DeveRetornar401()
    {
        var messageSid = $"SM{Guid.NewGuid():N}";
        await CriarConsultaComLembreteEnviadoAsync(messageSid);
        var parametros = new Dictionary<string, string>
        {
            ["OriginalRepliedMessageSid"] = messageSid,
            ["ButtonPayload"] = "confirmar"
        };

        var resposta = await EnviarWebhookAsync(parametros, assinatura: "assinatura-forjada");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Webhook_ComBotaoConfirmar_DeveMudarStatusParaConfirmadaEResponderAoPaciente()
    {
        var messageSid = $"SM{Guid.NewGuid():N}";
        var consultaId = await CriarConsultaComLembreteEnviadoAsync(messageSid);
        var parametros = new Dictionary<string, string>
        {
            ["From"] = "whatsapp:+5541988887766",
            ["OriginalRepliedMessageSid"] = messageSid,
            ["ButtonPayload"] = "confirmar"
        };

        var resposta = await EnviarWebhookAsync(parametros);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var consulta = await db.Consultas.FindAsync(consultaId);
        Assert.Equal(StatusConsulta.Confirmada, consulta!.Status);
        var mensagem = Assert.Single(_whatsApp.MensagensEnviadas);
        Assert.Equal("+5541988887766", mensagem.Telefone);
        Assert.Contains("confirmada", mensagem.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ComBotaoRemarcar_DeveMudarStatusParaCanceladaEResponderAoPaciente()
    {
        var messageSid = $"SM{Guid.NewGuid():N}";
        var consultaId = await CriarConsultaComLembreteEnviadoAsync(messageSid);
        var parametros = new Dictionary<string, string>
        {
            ["From"] = "whatsapp:+5541988887766",
            ["OriginalRepliedMessageSid"] = messageSid,
            ["ButtonPayload"] = "remarcar"
        };

        var resposta = await EnviarWebhookAsync(parametros);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var consulta = await db.Consultas.FindAsync(consultaId);
        Assert.Equal(StatusConsulta.Cancelada, consulta!.Status);
        var mensagem = Assert.Single(_whatsApp.MensagensEnviadas);
        Assert.Equal("+5541988887766", mensagem.Telefone);
        Assert.Contains("cancelada", mensagem.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ComMessageSidDesconhecido_DeveRetornar200SemAlterarNada()
    {
        var parametros = new Dictionary<string, string>
        {
            ["OriginalRepliedMessageSid"] = $"SM{Guid.NewGuid():N}",
            ["ButtonPayload"] = "confirmar"
        };

        var resposta = await EnviarWebhookAsync(parametros);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
