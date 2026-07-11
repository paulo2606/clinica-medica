using AgendamentoClinica.Api.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization.DataTypes;

namespace AgendamentoClinica.Api.Utils;

public static class RecorrenciaBloqueio
{
    public static string? MontarRegra(TipoRecorrenciaBloqueio tipo, DateOnly? recorrenciaAte)
    {
        if (tipo == TipoRecorrenciaBloqueio.Nenhuma)
        {
            return null;
        }

        var frequencia = tipo switch
        {
            TipoRecorrenciaBloqueio.Diaria => FrequencyType.Daily,
            TipoRecorrenciaBloqueio.Semanal => FrequencyType.Weekly,
            TipoRecorrenciaBloqueio.Mensal => FrequencyType.Monthly,
            TipoRecorrenciaBloqueio.Anual => FrequencyType.Yearly,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

        var padrao = new RecurrencePattern(frequencia);
        if (recorrenciaAte.HasValue)
        {
            padrao.Until = new CalDateTime(recorrenciaAte.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        }

        return new RecurrencePatternSerializer().SerializeToString(padrao);
    }

    public static bool OcorreEm(
        DateTime dataHoraInicio, DateTime dataHoraFim, string? regraRecorrencia, DateTime periodoInicio, DateTime periodoFim)
    {
        if (string.IsNullOrEmpty(regraRecorrencia))
        {
            return dataHoraInicio < periodoFim && periodoInicio < dataHoraFim;
        }

        var padrao = new RecurrencePatternSerializer().Deserialize(new StringReader(regraRecorrencia)) as RecurrencePattern;
        var evento = new CalendarEvent
        {
            Start = new CalDateTime(dataHoraInicio),
            End = new CalDateTime(dataHoraFim),
            RecurrenceRules = padrao is null ? [] : [padrao]
        };

        var duracao = dataHoraFim - dataHoraInicio;
        var buscaDesde = new CalDateTime(periodoInicio - duracao);

        return evento.GetOccurrences(buscaDesde)
            .TakeWhile(o => o.Period.StartTime.AsUtc < periodoFim)
            .Any(o => periodoInicio < o.Period.StartTime.AsUtc.Add(duracao));
    }
}
