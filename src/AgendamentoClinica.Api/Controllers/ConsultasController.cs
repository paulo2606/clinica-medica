using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendamentoClinica.Api.Dtos;
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

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarConsultaRequest requisicao)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var (resultado, id) = await _consultaService.CriarAsync(
            requisicao.PacienteId, requisicao.MedicoId, requisicao.DataHora, requisicao.Observacoes, usuarioId);

        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => BadRequest(new { mensagem = "Paciente ou médico não encontrado ou inativo." }),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Horário indisponível." }),
            ResultadoOperacao.ConflitoConcorrencia => Conflict(new { mensagem = "Horário acabou de ser ocupado, escolha outro." }),
            _ => Created(string.Empty, new { id })
        };
    }

    [HttpPatch("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var resultado = await _consultaService.CancelarAsync(id);
        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }

    [HttpPatch("{id:guid}/reagendar")]
    public async Task<IActionResult> Reagendar(Guid id, [FromBody] ReagendarConsultaRequest requisicao)
    {
        var resultado = await _consultaService.ReagendarAsync(id, requisicao.NovaDataHora);

        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => NotFound(),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Horário indisponível." }),
            ResultadoOperacao.ConflitoConcorrencia => Conflict(new { mensagem = "Horário acabou de ser ocupado, escolha outro." }),
            _ => NoContent()
        };
    }
}
