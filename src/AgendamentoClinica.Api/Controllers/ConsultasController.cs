using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/consultas")]
[Authorize(Roles = "Admin,Recepcao")]
public class ConsultasController : ControllerBase
{
    private readonly IConsultaService _consultaService;

    public ConsultasController(IConsultaService consultaService)
    {
        _consultaService = consultaService;
    }

    [HttpGet("horarios-livres")]
    public async Task<IActionResult> HorariosLivres([FromQuery] Guid medicoId, [FromQuery] DateOnly data)
    {
        var horarios = await _consultaService.CalcularHorariosLivresAsync(medicoId, data);
        return Ok(horarios);
    }
}
