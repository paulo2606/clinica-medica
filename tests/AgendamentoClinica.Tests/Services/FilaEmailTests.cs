using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class FilaEmailTests
{
    [Fact]
    public void Enfileirar_DevePermitirLeituraDaMensagem()
    {
        var fila = new FilaEmail();
        var mensagem = new EmailMensagem("bruno@clinica.com", "Assunto", "Corpo");

        fila.Enfileirar(mensagem);

        Assert.True(fila.Leitor.TryRead(out var lida));
        Assert.Equal(mensagem, lida);
    }
}
