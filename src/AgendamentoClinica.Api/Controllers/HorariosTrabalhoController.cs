using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/horarios-trabalho")]
[Authorize(Roles = "Admin,Recepcao")]
public class HorariosTrabalhoController : ControllerBase
{
    private readonly IHorarioTrabalhoService _horarioTrabalhoService;

    public HorariosTrabalhoController(IHorarioTrabalhoService horarioTrabalhoService)
    {
        _horarioTrabalhoService = horarioTrabalhoService;
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarHorarioTrabalhoRequest requisicao)
    {
        var (resultado, id) = await _horarioTrabalhoService.CriarAsync(
            requisicao.MedicoId, requisicao.DiaSemana, requisicao.HoraInicio, requisicao.HoraFim);

        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => BadRequest(new { mensagem = "Médico não encontrado ou inativo." }),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Horário se sobrepõe a outro já cadastrado nesse dia." }),
            _ => Created(string.Empty, new { id })
        };
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid medicoId)
    {
        var horarios = await _horarioTrabalhoService.ListarPorMedicoAsync(medicoId);
        return Ok(horarios.Select(h => new { h.Id, h.MedicoId, h.DiaSemana, h.HoraInicio, h.HoraFim }));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarHorarioTrabalhoRequest requisicao)
    {
        var resultado = await _horarioTrabalhoService.AtualizarAsync(
            id, requisicao.DiaSemana, requisicao.HoraInicio, requisicao.HoraFim);

        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => NotFound(),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Horário se sobrepõe a outro já cadastrado nesse dia." }),
            _ => NoContent()
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var resultado = await _horarioTrabalhoService.RemoverAsync(id);
        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }
}
