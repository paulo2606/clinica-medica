namespace AgendamentoClinica.Api.Models;

public class Medico
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid EspecialidadeId { get; set; }
    public string Crm { get; set; } = string.Empty;
    public int DuracaoConsultaPadraoMinutos { get; set; } = 15;
    public bool Ativo { get; set; } = true;

    public Usuario? Usuario { get; set; }
    public Especialidade? Especialidade { get; set; }
}
