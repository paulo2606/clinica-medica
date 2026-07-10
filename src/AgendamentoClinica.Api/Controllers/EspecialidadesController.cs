using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/especialidades")]
[Authorize(Roles = "Admin")]
public class EspecialidadesController : ControllerBase
{
    private readonly IEspecialidadeService _especialidadeService;

    public EspecialidadesController(IEspecialidadeService especialidadeService)
    {
        _especialidadeService = especialidadeService;
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarEspecialidadeRequest requisicao)
    {
        var id = await _especialidadeService.CriarAsync(requisicao.Nome);
        if (id is null)
        {
            return BadRequest(new { mensagem = "Já existe uma especialidade com esse nome." });
        }

        return Created(string.Empty, new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInativas = false)
    {
        var especialidades = await _especialidadeService.ListarAsync(incluirInativas);
        return Ok(especialidades.Select(e => new { e.Id, e.Nome, e.Ativo }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id)
    {
        var especialidade = await _especialidadeService.ObterAsync(id);
        if (especialidade is null)
        {
            return NotFound();
        }

        return Ok(new { especialidade.Id, especialidade.Nome, especialidade.Ativo });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarEspecialidadeRequest requisicao)
    {
        var resultado = await _especialidadeService.AtualizarAsync(id, requisicao.Nome);
        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => NotFound(),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Já existe uma especialidade com esse nome." }),
            _ => NoContent()
        };
    }

    [HttpPatch("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        var resultado = await _especialidadeService.DesativarAsync(id);
        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }
}
