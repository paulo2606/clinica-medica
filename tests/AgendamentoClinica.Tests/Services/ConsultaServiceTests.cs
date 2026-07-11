using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgendamentoClinica.Tests.Services;

public class ConsultaServiceTests
{
    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    private static async Task<Guid> CriarMedicoAsync(AgendamentoDbContext db)
    {
        var especialidade = new Especialidade { Id = Guid.NewGuid(), Nome = $"Especialidade-{Guid.NewGuid()}" };
        db.Especialidades.Add(especialidade);
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Bruno Medico",
            Email = $"{Guid.NewGuid()}@clinica.com",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            SenhaHash = "hash",
            Papel = PapelUsuario.Medico,
            Ativo = true
        };
        db.Usuarios.Add(usuario);
        var medico = new Medico { Id = Guid.NewGuid(), UsuarioId = usuario.Id, EspecialidadeId = especialidade.Id, Crm = $"CRM{Random.Shared.Next(1000000, 9999999)}" };
        db.Medicos.Add(medico);
        await db.SaveChangesAsync();
        return medico.Id;
    }

    private static async Task<Guid> CriarPacienteAsync(AgendamentoDbContext db)
    {
        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Paciente Teste",
            Cpf = $"{Random.Shared.NextInt64(10000000000, 99999999999)}",
            Telefone = $"{Random.Shared.Next(10000000, 99999999)}",
            DataNascimento = new DateOnly(1990, 1, 1)
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        return paciente.Id;
    }

    // 2026-07-13 é uma segunda-feira.
    private static readonly DateOnly DataTeste = new(2026, 7, 13);

    [Fact]
    public async Task CalcularHorariosLivresAsync_SemHorarioDeTrabalho_DeveRetornarListaVazia()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));

        var slots = await servico.CalcularHorariosLivresAsync(medicoId, DataTeste);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task CalcularHorariosLivresAsync_ComJanelaDeUmaHora_DeveGerarTresSlots()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(9, 0)
        });
        await db.SaveChangesAsync();
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));

        var slots = await servico.CalcularHorariosLivresAsync(medicoId, DataTeste);

        Assert.Equal(3, slots.Count);
        Assert.Equal(new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), slots[0]);
        Assert.Equal(new DateTime(2026, 7, 13, 8, 20, 0, DateTimeKind.Utc), slots[1]);
        Assert.Equal(new DateTime(2026, 7, 13, 8, 40, 0, DateTimeKind.Utc), slots[2]);
    }

    [Fact]
    public async Task CalcularHorariosLivresAsync_ComConsultaMarcada_DeveExcluirOSlotOcupado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(9, 0)
        });
        db.Consultas.Add(new Consulta
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            PacienteId = pacienteId,
            DataHora = new DateTime(2026, 7, 13, 8, 20, 0, DateTimeKind.Utc),
            DuracaoMinutos = 15,
            Status = StatusConsulta.Agendada,
            CriadoPorUsuarioId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));

        var slots = await servico.CalcularHorariosLivresAsync(medicoId, DataTeste);

        Assert.Equal(2, slots.Count);
        Assert.DoesNotContain(new DateTime(2026, 7, 13, 8, 20, 0, DateTimeKind.Utc), slots);
    }

    [Fact]
    public async Task CalcularHorariosLivresAsync_ComConsultaCancelada_NaoDeveExcluirOSlot()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(9, 0)
        });
        db.Consultas.Add(new Consulta
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            PacienteId = pacienteId,
            DataHora = new DateTime(2026, 7, 13, 8, 20, 0, DateTimeKind.Utc),
            DuracaoMinutos = 15,
            Status = StatusConsulta.Cancelada,
            CriadoPorUsuarioId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));

        var slots = await servico.CalcularHorariosLivresAsync(medicoId, DataTeste);

        Assert.Equal(3, slots.Count);
    }

    [Fact]
    public async Task CalcularHorariosLivresAsync_ComBloqueioDeAgenda_DeveExcluirOsSlotsBloqueados()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(),
            MedicoId = medicoId,
            DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(9, 0)
        });
        await db.SaveChangesAsync();
        var bloqueioService = new BloqueioAgendaService(db);
        await bloqueioService.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 30, 0, DateTimeKind.Utc), new DateTime(2026, 7, 13, 9, 0, 0, DateTimeKind.Utc),
            TipoRecorrenciaBloqueio.Nenhuma, null, "Compromisso");
        var servico = new ConsultaService(db, bloqueioService);

        var slots = await servico.CalcularHorariosLivresAsync(medicoId, DataTeste);

        Assert.Equal(new[] { new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc) }, slots);
    }

    [Fact]
    public async Task CalcularHorariosLivresAsync_ComDoisBlocosNoMesmoDia_DeveGerarSlotsDosDois()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(), MedicoId = medicoId, DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(8, 20)
        });
        db.HorariosTrabalhoMedico.Add(new HorarioTrabalhoMedico
        {
            Id = Guid.NewGuid(), MedicoId = medicoId, DiaSemana = DiaSemana.Segunda,
            HoraInicio = new TimeOnly(14, 0), HoraFim = new TimeOnly(14, 20)
        });
        await db.SaveChangesAsync();
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));

        var slots = await servico.CalcularHorariosLivresAsync(medicoId, DataTeste);

        Assert.Equal(2, slots.Count);
        Assert.Contains(new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), slots);
        Assert.Contains(new DateTime(2026, 7, 13, 14, 0, 0, DateTimeKind.Utc), slots);
    }

    [Fact]
    public async Task CriarAsync_ComHorarioLivre_DeveCriarConsulta()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));

        var (resultado, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), "Primeira consulta", Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var consulta = await db.Consultas.FindAsync(id);
        Assert.NotNull(consulta);
        Assert.Equal(15, consulta!.DuracaoMinutos);
        Assert.Equal(StatusConsulta.Agendada, consulta.Status);
    }

    [Fact]
    public async Task CriarAsync_ComPacienteInexistente_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));

        var (resultado, id) = await servico.CriarAsync(
            Guid.NewGuid(), medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CriarAsync_ComHorarioJaOcupado_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));
        await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        var (resultado, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 5, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CriarAsync_ComHorarioBloqueado_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var bloqueioService = new BloqueioAgendaService(db);
        await bloqueioService.CriarAsync(
            medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 13, 9, 0, 0, DateTimeKind.Utc),
            TipoRecorrenciaBloqueio.Nenhuma, null, "Bloqueado");
        var servico = new ConsultaService(db, bloqueioService);

        var (resultado, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
        Assert.Null(id);
    }

    [Fact]
    public async Task CancelarAsync_ComIdExistente_DeveMarcarComoCancelada()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));
        var (_, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        var resultado = await servico.CancelarAsync(id!.Value);

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var consulta = await db.Consultas.FindAsync(id);
        Assert.Equal(StatusConsulta.Cancelada, consulta!.Status);
    }

    [Fact]
    public async Task CancelarAsync_LiberaOHorarioParaNovaConsulta()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));
        var (_, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());
        await servico.CancelarAsync(id!.Value);

        var (resultado, novoId) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        Assert.NotNull(novoId);
    }

    [Fact]
    public async Task ReagendarAsync_ParaHorarioLivre_DeveAtualizarDataHora()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));
        var (_, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        var resultado = await servico.ReagendarAsync(id!.Value, new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        var consulta = await db.Consultas.FindAsync(id);
        Assert.Equal(new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc), consulta!.DataHora);
    }

    [Fact]
    public async Task ReagendarAsync_ParaHorarioOcupadoPorOutraConsulta_DeveRetornarDuplicado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));
        await servico.CriarAsync(pacienteId, medicoId, new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());
        var (_, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());

        var resultado = await servico.ReagendarAsync(id!.Value, new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ResultadoOperacao.Duplicado, resultado);
    }

    [Fact]
    public async Task ReagendarAsync_ComConsultaCancelada_DeveRetornarNaoEncontrado()
    {
        var db = CriarDbContext();
        var medicoId = await CriarMedicoAsync(db);
        var pacienteId = await CriarPacienteAsync(db);
        var servico = new ConsultaService(db, new BloqueioAgendaService(db));
        var (_, id) = await servico.CriarAsync(
            pacienteId, medicoId, new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());
        await servico.CancelarAsync(id!.Value);

        var resultado = await servico.ReagendarAsync(id.Value, new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ResultadoOperacao.NaoEncontrado, resultado);
    }
}
