using System.Net;
using System.Text.Json;

namespace AgendamentoClinica.Api.Middleware;

public class TratamentoErroMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<TratamentoErroMiddleware> _logger;

    public TratamentoErroMiddleware(RequestDelegate proximo, ILogger<TratamentoErroMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado ao processar {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);

            contexto.Response.ContentType = "application/json";
            contexto.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await contexto.Response.WriteAsync(JsonSerializer.Serialize(new { mensagem = "Ocorreu um erro inesperado." }));
        }
    }
}
