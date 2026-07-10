namespace AgendamentoClinica.Api.Models;

public record EmailMensagem(
    string Para, 
    string Assunto, 
    string Corpo
    );
