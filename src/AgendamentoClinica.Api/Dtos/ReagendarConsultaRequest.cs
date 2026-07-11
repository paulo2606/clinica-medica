using System.ComponentModel.DataAnnotations;

namespace AgendamentoClinica.Api.Dtos;

public record ReagendarConsultaRequest(
    [Required] DateTime NovaDataHora
    );
