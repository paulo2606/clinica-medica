namespace AgendamentoClinica.Api.Dtos;

public record LoginResponse(
    string AccessToken, 
    string RefreshToken
    );
