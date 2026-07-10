using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/medicos")]
public class MedicosController : ControllerBase
{
    private readonly IMedicoService _medicoService;

    public MedicosController(IMedicoService medicoService)
    {
        _medicoService = medicoService;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarMedicoRequest requisicao)
    {
        var (resultado, id) = await _medicoService.CriarAsync(
            requisicao.Nome, requisicao.Email, requisicao.Telefone, requisicao.Crm, requisicao.EspecialidadeId);

        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => BadRequest(new { mensagem = "Especialidade não encontrada ou inativa." }),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "E-mail, telefone ou CRM já cadastrado." }),
            _ => Created(string.Empty, new { id })
        };
    }

    [Authorize(Roles = "Admin,Recepcao")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInativos = false)
    {
        var medicos = await _medicoService.ListarAsync(incluirInativos);
        return Ok(medicos.Select(MapearParaResposta));
    }

    [Authorize(Roles = "Admin,Recepcao")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id)
    {
        var medico = await _medicoService.ObterAsync(id);
        return medico is null ? NotFound() : Ok(MapearParaResposta(medico));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarMedicoRequest requisicao)
    {
        var resultado = await _medicoService.AtualizarAsync(id, requisicao.Crm, requisicao.EspecialidadeId);
        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => NotFound(),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Já existe um médico cadastrado com esse CRM." }),
            _ => NoContent()
        };
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        var resultado = await _medicoService.DesativarAsync(id);
        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }

    private static object MapearParaResposta(Models.Medico medico) => new
    {
        medico.Id,
        medico.Crm,
        medico.DuracaoConsultaPadraoMinutos,
        medico.Ativo,
        Usuario = new { medico.Usuario!.Id, medico.Usuario.Nome, medico.Usuario.Email, medico.Usuario.Telefone, medico.Usuario.Ativo },
        Especialidade = new { medico.Especialidade!.Id, medico.Especialidade.Nome }
    };
}
