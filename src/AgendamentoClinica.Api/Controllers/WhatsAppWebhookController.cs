using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Twilio.Security;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IConsultaService _consultaService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IConfiguration _configuracao;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConsultaService consultaService, IWhatsAppService whatsAppService, IConfiguration configuracao, ILogger<WhatsAppWebhookController> logger)
    {
        _consultaService = consultaService;
        _whatsAppService = whatsAppService;
        _configuracao = configuracao;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var parametros = Request.Form.ToDictionary(p => p.Key, p => p.Value.ToString());
        var assinatura = Request.Headers["X-Twilio-Signature"].ToString();
        var url = $"{Request.Scheme}://{Request.Host}{Request.Path}";

        var authToken = _configuracao["Twilio:AuthToken"];
        if (string.IsNullOrEmpty(authToken))
        {
            _logger.LogError("Twilio:AuthToken não configurado — rejeitando webhook do WhatsApp.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        var validador = new RequestValidator(authToken);
        if (!validador.Validate(url, parametros, assinatura))
        {
            _logger.LogWarning(
                "Assinatura inválida no webhook do WhatsApp. IP: {Ip}", HttpContext.Connection.RemoteIpAddress);
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

        string? respostaAoPaciente = null;
        if (acao == "confirmar")
        {
            await _consultaService.ConfirmarAsync(consulta.Id);
            respostaAoPaciente = $"Consulta confirmada para {consulta.DataHora:dd/MM/yyyy} às {consulta.DataHora:HH:mm}. Te esperamos!";
        }
        else if (acao == "remarcar")
        {
            await _consultaService.CancelarAsync(consulta.Id);
            respostaAoPaciente = "Sua consulta foi cancelada. Entre em contato com a clínica se quiser remarcar.";
        }
        else
        {
            _logger.LogWarning("ButtonPayload desconhecido recebido no webhook do WhatsApp: {Acao}", acao);
        }

        if (respostaAoPaciente is not null && parametros.TryGetValue("From", out var telefoneOrigem))
        {
            try
            {
                await _whatsAppService.EnviarMensagemLivreAsync(telefoneOrigem.Replace("whatsapp:", ""), respostaAoPaciente);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar confirmação de resposta pro paciente da consulta {ConsultaId}", consulta.Id);
            }
        }

        return Ok();
    }
}
