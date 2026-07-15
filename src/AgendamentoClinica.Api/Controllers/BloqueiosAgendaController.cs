using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/bloqueios-agenda")]
[Authorize]
public class BloqueiosAgendaController : ControllerBase
{
    private readonly IBloqueioAgendaService _bloqueioAgendaService;
    private readonly IMedicoService _medicoService;

    public BloqueiosAgendaController(IBloqueioAgendaService bloqueioAgendaService, IMedicoService medicoService)
    {
        _bloqueioAgendaService = bloqueioAgendaService;
        _medicoService = medicoService;
    }

    [Authorize(Roles = "Admin,Recepcao")]
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarBloqueioAgendaRequest requisicao)
    {
        var (resultado, id) = await _bloqueioAgendaService.CriarAsync(
            requisicao.MedicoId, requisicao.DataHoraInicio, requisicao.DataHoraFim,
            requisicao.TipoRecorrencia, requisicao.RecorrenciaAte, requisicao.Motivo, requisicao.Cor);

        return resultado == ResultadoOperacao.NaoEncontrado
            ? BadRequest(new { mensagem = "Médico não encontrado ou inativo." })
            : Created(string.Empty, new { id });
    }

    [Authorize(Roles = "Medico")]
    [HttpPost("meus")]
    public async Task<IActionResult> CriarMeu([FromBody] CriarMeuBloqueioRequest requisicao)
    {
        var medicoId = await ResolverMeuMedicoIdAsync();
        if (medicoId is null)
        {
            return NotFound(new { mensagem = "Cadastro de médico não encontrado pra esse usuário." });
        }

        var (resultado, id) = await _bloqueioAgendaService.CriarAsync(
            medicoId.Value, requisicao.DataHoraInicio, requisicao.DataHoraFim,
            requisicao.TipoRecorrencia, requisicao.RecorrenciaAte, requisicao.Motivo, requisicao.Cor);

        return resultado == ResultadoOperacao.NaoEncontrado
            ? BadRequest(new { mensagem = "Médico não encontrado ou inativo." })
            : Created(string.Empty, new { id });
    }

    [Authorize(Roles = "Admin,Recepcao")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid medicoId)
    {
        var bloqueios = await _bloqueioAgendaService.ListarPorMedicoAsync(medicoId);
        return Ok(bloqueios.Select(MapearParaResposta));
    }

    [Authorize(Roles = "Medico")]
    [HttpGet("meus")]
    public async Task<IActionResult> ListarMeus()
    {
        var medicoId = await ResolverMeuMedicoIdAsync();
        if (medicoId is null)
        {
            return NotFound(new { mensagem = "Cadastro de médico não encontrado pra esse usuário." });
        }

        var bloqueios = await _bloqueioAgendaService.ListarPorMedicoAsync(medicoId.Value);
        return Ok(bloqueios.Select(MapearParaResposta));
    }

    [Authorize(Roles = "Admin,Recepcao,Medico")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarBloqueioAgendaRequest requisicao)
    {
        var (permitido, medicoIdRestricao) = await TentarResolverRestricaoDeMedicoAsync();
        if (!permitido)
        {
            return NotFound();
        }

        var resultado = await _bloqueioAgendaService.AtualizarAsync(
            id, medicoIdRestricao, requisicao.DataHoraInicio, requisicao.DataHoraFim,
            requisicao.TipoRecorrencia, requisicao.RecorrenciaAte, requisicao.Motivo, requisicao.Cor);

        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }

    [Authorize(Roles = "Admin,Recepcao,Medico")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var (permitido, medicoIdRestricao) = await TentarResolverRestricaoDeMedicoAsync();
        if (!permitido)
        {
            return NotFound();
        }

        var resultado = await _bloqueioAgendaService.RemoverAsync(id, medicoIdRestricao);

        return resultado == ResultadoOperacao.NaoEncontrado ? NotFound() : NoContent();
    }

    private async Task<Guid?> ResolverMeuMedicoIdAsync()
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        return await _medicoService.ObterMedicoIdPorUsuarioAsync(usuarioId);
    }

    private async Task<(bool Permitido, Guid? MedicoIdRestricao)> TentarResolverRestricaoDeMedicoAsync()
    {
        if (!User.IsInRole("Medico"))
        {
            return (true, null);
        }

        var medicoId = await ResolverMeuMedicoIdAsync();
        return (medicoId.HasValue, medicoId);
    }

    private static object MapearParaResposta(Models.BloqueioAgendaMedico bloqueio) => new
    {
        bloqueio.Id,
        bloqueio.MedicoId,
        bloqueio.DataHoraInicio,
        bloqueio.DataHoraFim,
        bloqueio.TipoRecorrencia,
        bloqueio.RecorrenciaAte,
        bloqueio.Motivo,
        bloqueio.Cor
    };
}
