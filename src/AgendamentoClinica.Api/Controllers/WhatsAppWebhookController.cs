using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Twilio.Security;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IConsultaService _consultaService;
    private readonly IConfiguration _configuracao;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConsultaService consultaService, IConfiguration configuracao, ILogger<WhatsAppWebhookController> logger)
    {
        _consultaService = consultaService;
        _configuracao = configuracao;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var parametros = Request.Form.ToDictionary(p => p.Key, p => p.Value.ToString());
        var assinatura = Request.Headers["X-Twilio-Signature"].ToString();
        var url = $"{Request.Scheme}://{Request.Host}{Request.Path}";

        var validador = new RequestValidator(_configuracao["Twilio:AuthToken"] ?? "");
        if (!validador.Validate(url, parametros, assinatura))
        {
            return Unauthorized();
        }

        if (!parametros.TryGetValue("OriginalRepliedMessageSid", out var messageSid)
            || !parametros.TryGetValue("ButtonPayload", out var acao))
        {
            return Ok();
        }

        var consulta = await _consultaService.ObterPorLembreteMessageSidAsync(messageSid);
        if (consulta is null)
        {
            return Ok();
        }

        if (acao == "confirmar")
        {
            await _consultaService.ConfirmarAsync(consulta.Id);
        }
        else if (acao == "cancelar")
        {
            await _consultaService.CancelarAsync(consulta.Id);
        }
        else
        {
            _logger.LogWarning("ButtonPayload desconhecido recebido no webhook do WhatsApp: {Acao}", acao);
        }

        return Ok();
    }
}
