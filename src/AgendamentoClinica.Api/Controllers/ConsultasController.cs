using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendamentoClinica.Api.Dtos;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using AgendamentoClinica.Api.Utils;
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
    private readonly IAnexoConsultaService _anexoConsultaService;

    public ConsultasController(IConsultaService consultaService, IMedicoService medicoService, IAnexoConsultaService anexoConsultaService)
    {
        _consultaService = consultaService;
        _medicoService = medicoService;
        _anexoConsultaService = anexoConsultaService;
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
    [HttpGet("dias-disponiveis")]
    public async Task<IActionResult> DiasDisponiveis([FromQuery] Guid medicoId, [FromQuery] int ano, [FromQuery] int mes)
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

        var dias = await _consultaService.CalcularDiasDisponiveisAsync(medicoIdEfetivo, ano, mes);
        return Ok(dias);
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
            requisicao.PacienteId, medicoId, requisicao.DataHora, requisicao.Observacoes, usuarioId, requisicao.Tipo);

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

    [Authorize(Roles = "Admin,Recepcao,Medico")]
    [RequestSizeLimit(ValidadorAnexo.TamanhoMaximoBytes)]
    [HttpPost("{id:guid}/anexos")]
    public async Task<IActionResult> AdicionarAnexo(Guid id, IFormFile arquivo)
    {
        var (permitido, medicoIdRestricao) = await TentarResolverRestricaoDeMedicoAsync();
        if (!permitido)
        {
            return NotFound();
        }

        if (arquivo is null || !ValidadorAnexo.TamanhoValido(arquivo.Length))
        {
            return BadRequest(new { mensagem = "Envie um arquivo PDF, JPEG ou PNG de até 5MB." });
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria);
        var conteudo = memoria.ToArray();

        var extensao = ValidadorAnexo.DetectarExtensao(conteudo);
        if (extensao is null)
        {
            return BadRequest(new { mensagem = "Formato inválido. Envie um arquivo PDF, JPEG ou PNG." });
        }

        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var (resultado, anexoId) = await _anexoConsultaService.AdicionarAsync(
            id, medicoIdRestricao, usuarioId, conteudo, extensao, arquivo.FileName);

        return resultado switch
        {
            ResultadoOperacao.NaoEncontrado => NotFound(),
            ResultadoOperacao.LimiteExcedido => BadRequest(new { mensagem = "Essa consulta já tem o número máximo de anexos." }),
            _ => Created(string.Empty, new { id = anexoId })
        };
    }

    [Authorize(Roles = "Admin,Medico")]
    [HttpGet("{id:guid}/anexos")]
    public async Task<IActionResult> ListarAnexos(Guid id)
    {
        var (permitido, medicoId) = await TentarResolverRestricaoDeMedicoAsync();
        if (!permitido)
        {
            return NotFound();
        }

        var (resultado, anexos) = await _anexoConsultaService.ListarAsync(id, medicoId);
        if (resultado == ResultadoOperacao.NaoEncontrado)
        {
            return NotFound();
        }

        return Ok(anexos.Select(a => new { a.Id, a.NomeOriginal, a.Extensao, a.TamanhoBytes, a.CriadoEm }));
    }

    [Authorize(Roles = "Admin,Medico")]
    [HttpGet("{id:guid}/anexos/{anexoId:guid}")]
    public async Task<IActionResult> BaixarAnexo(Guid id, Guid anexoId)
    {
        var (permitido, medicoId) = await TentarResolverRestricaoDeMedicoAsync();
        if (!permitido)
        {
            return NotFound();
        }

        var (resultado, conteudo, anexo) = await _anexoConsultaService.ObterConteudoAsync(id, anexoId, medicoId);
        if (resultado == ResultadoOperacao.NaoEncontrado || conteudo is null || anexo is null)
        {
            return NotFound();
        }

        var contentType = anexo.Extensao switch
        {
            "pdf" => "application/pdf",
            "jpg" => "image/jpeg",
            "png" => "image/png",
            _ => "application/octet-stream"
        };

        return File(conteudo, contentType, anexo.NomeOriginal);
    }

    [Authorize(Roles = "Admin,Medico")]
    [HttpDelete("{id:guid}/anexos/{anexoId:guid}")]
    public async Task<IActionResult> RemoverAnexo(Guid id, Guid anexoId)
    {
        var (permitido, medicoId) = await TentarResolverRestricaoDeMedicoAsync();
        if (!permitido)
        {
            return NotFound();
        }

        var resultado = await _anexoConsultaService.RemoverAsync(id, anexoId, medicoId);
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
}
