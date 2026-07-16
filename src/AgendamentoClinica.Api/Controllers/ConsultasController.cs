using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/consultas")]
[Authorize]
public class ConsultasController : ControllerBase
{
    private readonly IConsultaService _consultaService;
    private readonly IMedicoService _medicoService;

    public ConsultasController(IConsultaService consultaService, IMedicoService medicoService)
    {
        _consultaService = consultaService;
        _medicoService = medicoService;
    }

    [Authorize(Roles = "Admin,Recepcao,Medico")]
    [HttpGet("horarios-livres")]
    public async Task<IActionResult> HorariosLivres([FromQuery] Guid medicoId, [FromQuery] DateOnly data)
    {
        var medicoIdEfetivo = medicoId;
        if (User.IsInRole("Medico"))
        {
            var meuMedicoId = await ResolverMeuMedicoIdAsync();
            if (meuMedicoId is null)
            {
                return NotFound(new { mensagem = "Cadastro de médico não encontrado pra esse usuário." });
            }
            medicoIdEfetivo = meuMedicoId.Value;
        }

        var horarios = await _consultaService.CalcularHorariosLivresAsync(medicoIdEfetivo, data);
        return Ok(horarios);
    }

    [Authorize(Roles = "Admin,Recepcao,Medico")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? medicoId, [FromQuery] DateOnly? data, [FromQuery] StatusConsulta? status)
    {
        var medicoIdEfetivo = medicoId;
        if (User.IsInRole("Medico"))
        {
            medicoIdEfetivo = await ResolverMeuMedicoIdAsync();
            if (medicoIdEfetivo is null)
            {
                return NotFound(new { mensagem = "Cadastro de médico não encontrado pra esse usuário." });
            }
        }

        var consultas = await _consultaService.ListarAsync(medicoIdEfetivo, data, status);

        return Ok(consultas.Select(c => new
        {
            c.Id,
            c.PacienteId,
            PacienteNome = c.Paciente!.Nome,
            c.MedicoId,
            MedicoNome = c.Medico!.Usuario!.Nome,
            c.DataHora,
            c.DuracaoMinutos,
            c.Status,
            c.Observacoes
        }));
    }

    [Authorize(Roles = "Admin,Recepcao,Medico")]
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarConsultaRequest requisicao)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var medicoId = requisicao.MedicoId;
        if (User.IsInRole("Medico"))
        {
            var meuMedicoId = await ResolverMeuMedicoIdAsync();
            if (meuMedicoId is null)
            {
                return NotFound(new { mensagem = "Cadastro de médico não encontrado pra esse usuário." });
            }
            medicoId = meuMedicoId.Value;
        }

        var (resultado, id) = await _consultaService.CriarAsync(
            requisicao.PacienteId, medicoId, requisicao.DataHora, requisicao.Observacoes, usuarioId);

        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => BadRequest(new { mensagem = "Paciente ou médico não encontrado ou inativo." }),
            ResultadoOperacao.Duplicado => BadRequest(new { mensagem = "Horário indisponível." }),
            ResultadoOperacao.ConflitoConcorrencia => Conflict(new { mensagem = "Horário acabou de ser ocupado, escolha outro." }),
            _ => Created(string.Empty, new { id })
        };
    }

    [Authorize(Roles = "Admin,Recepcao")]
    [HttpPatch("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var resultado = await _consultaService.CancelarAsync(id);
        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }

    [Authorize(Roles = "Admin,Recepcao")]
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

    private async Task<Guid?> ResolverMeuMedicoIdAsync()
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        return await _medicoService.ObterMedicoIdPorUsuarioAsync(usuarioId);
    }
}
