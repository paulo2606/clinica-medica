using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Data;

public static class DbSeeder
{
    public static async Task SemearAdminInicialAsync(AgendamentoDbContext db, ISenhaService senhaService, IConfiguration configuracao)
    {
        if (await db.Usuarios.AnyAsync())
        {
            return;
        }

        var senhaInicial = configuracao["ADMIN_SENHA_INICIAL"];
        if (string.IsNullOrWhiteSpace(senhaInicial))
        {
            return;
        }

        var emailInicial = configuracao["ADMIN_EMAIL_INICIAL"];
        if (string.IsNullOrWhiteSpace(emailInicial))
        {
            emailInicial = "admin@clinica.com";
        }

        db.Usuarios.Add(new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Administrador",
            Email = emailInicial,
            Telefone = "",
            SenhaHash = senhaService.GerarHash(senhaInicial),
            Papel = PapelUsuario.Admin,
            Ativo = true
        });
        await db.SaveChangesAsync();
    }
}
