using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/pacientes")]
[Authorize(Roles = "Admin,Recepcao")]
public class PacientesController : ControllerBase
{
    private readonly IPacienteService _pacienteService;

    public PacientesController(IPacienteService pacienteService)
    {
        _pacienteService = pacienteService;
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarPacienteRequest requisicao)
    {
        var id = await _pacienteService.CriarAsync(
            requisicao.Nome, requisicao.Cpf, requisicao.Telefone, requisicao.Email, requisicao.DataNascimento);
        if (id is null)
        {
            return BadRequest(new { mensagem = "Já existe um paciente cadastrado com esse CPF." });
        }

        return Created(string.Empty, new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Buscar([FromQuery] string? cpf, [FromQuery] string? nome, [FromQuery] bool incluirInativos = false)
    {
        var pacientes = await _pacienteService.BuscarAsync(cpf, nome, incluirInativos);
        return Ok(pacientes.Select(p => new { p.Id, p.Nome, p.Cpf, p.Telefone, p.Email, p.DataNascimento, p.Ativo }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id)
    {
        var paciente = await _pacienteService.ObterAsync(id);
        if (paciente is null)
        {
            return NotFound();
        }

        return Ok(new { paciente.Id, paciente.Nome, paciente.Cpf, paciente.Telefone, paciente.Email, paciente.DataNascimento, paciente.Ativo });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarPacienteRequest requisicao)
    {
        var resultado = await _pacienteService.AtualizarAsync(
            id, requisicao.Nome, requisicao.Cpf, requisicao.Telefone, requisicao.Email, requisicao.DataNascimento);
        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => NotFound(),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Já existe um paciente cadastrado com esse CPF." }),
            _ => NoContent()
        };
    }

    [HttpPatch("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        var resultado = await _pacienteService.DesativarAsync(id);
        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }
}
