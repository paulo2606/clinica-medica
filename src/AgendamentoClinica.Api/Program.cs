using System.Text;
using System.Threading.RateLimiting;
using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Middleware;
using AgendamentoClinica.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<AgendamentoDbContext>(opcoes =>
    opcoes.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<ISenhaService, SenhaService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.MapInboundClaims = false;
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opcoes =>
{
    var limitePermitido = builder.Configuration.GetValue("RateLimiting:AuthSensivel:PermitLimit", 5);
    opcoes.AddPolicy("auth-sensivel", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limitePermitido,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    opcoes.OnRejected = async (contexto, ct) =>
    {
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await contexto.HttpContext.Response.WriteAsJsonAsync(
            new { mensagem = "Muitas tentativas. Tente novamente em instantes." }, ct);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo { Title = "AgendamentoClinica.Api", Version = "v1" });

    opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole aqui só o token (sem o prefixo 'Bearer ')."
    });
    opcoes.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<TratamentoErroMiddleware>();
app.Use(async (contexto, proximo) =>
{
    contexto.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    contexto.Response.Headers.Append("X-Frame-Options", "DENY");
    contexto.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await proximo();
});
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var escopoInicializacao = app.Services.CreateScope())
{
    var db = escopoInicializacao.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
    await db.Database.MigrateAsync();
    var senhaService = escopoInicializacao.ServiceProvider.GetRequiredService<ISenhaService>();
    await DbSeeder.SemearAdminInicialAsync(db, senhaService, app.Configuration);
}

app.Run();

public partial class Program { }
