using System.Text.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace AgendamentoClinica.Api.Services;

public class TwilioWhatsAppService : IWhatsAppService
{
    private readonly IConfiguration _configuracao;

    public TwilioWhatsAppService(IConfiguration configuracao)
    {
        _configuracao = configuracao;
        TwilioClient.Init(_configuracao["Twilio:AccountSid"], _configuracao["Twilio:AuthToken"]);
    }

    public async Task<string> EnviarLembreteConsultaAsync(
        string telefoneDestino, string nomePaciente, string data, string hora, string nomeMedico, string endereco)
    {
        var variaveis = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["1"] = nomePaciente,
            ["2"] = data,
            ["3"] = hora,
            ["4"] = nomeMedico,
            ["5"] = endereco
        });

        var mensagem = await MessageResource.CreateAsync(
            from: new PhoneNumber(_configuracao["Twilio:WhatsAppFrom"]),
            to: new PhoneNumber($"whatsapp:{telefoneDestino}"),
            contentSid: _configuracao["Twilio:ContentSid"],
            contentVariables: variaveis);

        return mensagem.Sid;
    }
}
