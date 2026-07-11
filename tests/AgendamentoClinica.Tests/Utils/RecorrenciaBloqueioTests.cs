using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Utils;
using Xunit;

namespace AgendamentoClinica.Tests.Utils;

public class RecorrenciaBloqueioTests
{
    [Fact]
    public void MontarRegra_ComTipoNenhuma_DeveRetornarNulo()
    {
        var regra = RecorrenciaBloqueio.MontarRegra(TipoRecorrenciaBloqueio.Nenhuma, null);

        Assert.Null(regra);
    }

    [Fact]
    public void MontarRegra_ComTipoSemanal_DeveRetornarRegraNaoNula()
    {
        var regra = RecorrenciaBloqueio.MontarRegra(TipoRecorrenciaBloqueio.Semanal, null);

        Assert.NotNull(regra);
        Assert.Contains("WEEKLY", regra);
    }

    [Fact]
    public void OcorreEm_SemRecorrencia_ComSobreposicao_DeveRetornarTrue()
    {
        var inicio = new DateTime(2026, 7, 13, 14, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 7, 13, 16, 0, 0, DateTimeKind.Utc);

        var ocorre = RecorrenciaBloqueio.OcorreEm(
            inicio, fim, null,
            new DateTime(2026, 7, 13, 15, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 13, 15, 20, 0, DateTimeKind.Utc));

        Assert.True(ocorre);
    }

    [Fact]
    public void OcorreEm_SemRecorrencia_SemSobreposicao_DeveRetornarFalse()
    {
        var inicio = new DateTime(2026, 7, 13, 14, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 7, 13, 16, 0, 0, DateTimeKind.Utc);

        var ocorre = RecorrenciaBloqueio.OcorreEm(
            inicio, fim, null,
            new DateTime(2026, 7, 14, 15, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 14, 15, 20, 0, DateTimeKind.Utc));

        Assert.False(ocorre);
    }

    [Fact]
    public void OcorreEm_ComRecorrenciaSemanal_DeveRepetirNoMesmoDiaDaSemana()
    {
        var primeiraOcorrencia = new DateTime(2026, 7, 11, 14, 0, 0, DateTimeKind.Utc);
        var fimPrimeiraOcorrencia = new DateTime(2026, 7, 11, 16, 0, 0, DateTimeKind.Utc);
        var regra = RecorrenciaBloqueio.MontarRegra(TipoRecorrenciaBloqueio.Semanal, null);

        var duasSemanasDepois = new DateTime(2026, 7, 25, 14, 30, 0, DateTimeKind.Utc);
        var ocorre = RecorrenciaBloqueio.OcorreEm(
            primeiraOcorrencia, fimPrimeiraOcorrencia, regra,
            duasSemanasDepois, duasSemanasDepois.AddMinutes(20));

        Assert.True(ocorre);
    }

    [Fact]
    public void OcorreEm_ComRecorrenciaSemanal_NaoDeveOcorrerEmOutroDiaDaSemana()
    {
        var primeiraOcorrencia = new DateTime(2026, 7, 11, 14, 0, 0, DateTimeKind.Utc);
        var fimPrimeiraOcorrencia = new DateTime(2026, 7, 11, 16, 0, 0, DateTimeKind.Utc);
        var regra = RecorrenciaBloqueio.MontarRegra(TipoRecorrenciaBloqueio.Semanal, null);

        var outroDia = new DateTime(2026, 7, 14, 14, 30, 0, DateTimeKind.Utc);
        var ocorre = RecorrenciaBloqueio.OcorreEm(
            primeiraOcorrencia, fimPrimeiraOcorrencia, regra,
            outroDia, outroDia.AddMinutes(20));

        Assert.False(ocorre);
    }

    [Fact]
    public void OcorreEm_ComRecorrenciaComDataLimite_NaoDeveOcorrerAposOLimite()
    {
        var primeiraOcorrencia = new DateTime(2026, 7, 11, 14, 0, 0, DateTimeKind.Utc);
        var fimPrimeiraOcorrencia = new DateTime(2026, 7, 11, 16, 0, 0, DateTimeKind.Utc);
        var regra = RecorrenciaBloqueio.MontarRegra(TipoRecorrenciaBloqueio.Semanal, new DateOnly(2026, 7, 18));

        var depoisDoLimite = new DateTime(2026, 7, 25, 14, 30, 0, DateTimeKind.Utc);
        var ocorre = RecorrenciaBloqueio.OcorreEm(
            primeiraOcorrencia, fimPrimeiraOcorrencia, regra,
            depoisDoLimite, depoisDoLimite.AddMinutes(20));

        Assert.False(ocorre);
    }

    [Fact]
    public void OcorreEm_ComRecorrenciaMensal_DeveRepetirNoMesmoDiaDoMes()
    {
        var primeiraOcorrencia = new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc);
        var fimPrimeiraOcorrencia = new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);
        var regra = RecorrenciaBloqueio.MontarRegra(TipoRecorrenciaBloqueio.Mensal, null);

        var mesSeguinte = new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc);
        var ocorre = RecorrenciaBloqueio.OcorreEm(
            primeiraOcorrencia, fimPrimeiraOcorrencia, regra,
            mesSeguinte, mesSeguinte.AddMinutes(20));

        Assert.True(ocorre);
    }
}
