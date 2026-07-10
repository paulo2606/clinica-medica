using AgendamentoClinica.Api.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AgendamentoClinica.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuracao;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuracao, ILogger<EmailService> logger)
    {
        _configuracao = configuracao;
        _logger = logger;
    }

    public async Task<bool> EnviarAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default)
    {
        try
        {
            var mensagemMime = new MimeMessage();
            mensagemMime.From.Add(MailboxAddress.Parse(_configuracao["Email:Remetente"]));
            mensagemMime.To.Add(MailboxAddress.Parse(mensagem.Para));
            mensagemMime.Subject = mensagem.Assunto;
            mensagemMime.Body = new TextPart("plain") { Text = mensagem.Corpo };

            using var cliente = new SmtpClient();
            await cliente.ConnectAsync(
                _configuracao["Email:Host"],
                int.Parse(_configuracao["Email:Porta"] ?? "587"),
                SecureSocketOptions.StartTls,
                cancellationToken);
            await cliente.AuthenticateAsync(_configuracao["Email:Usuario"], _configuracao["Email:Senha"], cancellationToken);
            await cliente.SendAsync(mensagemMime, cancellationToken);
            await cliente.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            // Falha de envio (SMTP fora do ar, credencial errada, rede) não pode
            // derrubar quem chamou — a FilaEmailBackgroundService decide se tenta
            // de novo. Nunca loga o corpo da mensagem: pode conter o token de convite.
            _logger.LogError(ex, "Falha ao enviar e-mail para {Destinatario}", mensagem.Para);
            return false;
        }
    }
}
