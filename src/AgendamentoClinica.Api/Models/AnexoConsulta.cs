namespace AgendamentoClinica.Api.Models;

public class AnexoConsulta
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public string NomeOriginal { get; set; } = "";
    public string Extensao { get; set; } = "";
    public long TamanhoBytes { get; set; }
    public Guid EnviadoPorUsuarioId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Consulta? Consulta { get; set; }
}
