using System.Threading.Channels;
using AgendamentoClinica.Api.Models;

namespace AgendamentoClinica.Api.Services;

public class FilaEmail : IFilaEmail
{
    private readonly Channel<EmailMensagem> _canal = Channel.CreateUnbounded<EmailMensagem>();

    public ChannelReader<EmailMensagem> Leitor => _canal.Reader;

    public void Enfileirar(EmailMensagem mensagem) => _canal.Writer.TryWrite(mensagem);
}
