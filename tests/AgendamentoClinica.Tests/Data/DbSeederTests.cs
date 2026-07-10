using AgendamentoClinica.Api.Data;
using AgendamentoClinica.Api.Models;
using AgendamentoClinica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgendamentoClinica.Tests.Data;

public class DbSeederTests
{
    private static AgendamentoDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AgendamentoDbContext(opcoes);
    }

    [Fact]
    public async Task SemearAdminInicialAsync_SemUsuarios_DeveCriarAdmin()
    {
        var db = CriarDbContext();
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ADMIN_SENHA_INICIAL"] = "senhaForte123" })
            .Build();

        await DbSeeder.SemearAdminInicialAsync(db, new SenhaService(), configuracao);

        Assert.Single(db.Usuarios);
        Assert.Equal(PapelUsuario.Admin, db.Usuarios.Single().Papel);
    }

    [Fact]
    public async Task SemearAdminInicialAsync_ComUsuariosExistentes_NaoDeveCriarNovoAdmin()
    {
        var db = CriarDbContext();
        db.Usuarios.Add(new Usuario { Id = Guid.NewGuid(), Nome = "Alguém", Email = "a@a.com", SenhaHash = "x", Papel = PapelUsuario.Recepcao });
        await db.SaveChangesAsync();
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ADMIN_SENHA_INICIAL"] = "senhaForte123" })
            .Build();

        await DbSeeder.SemearAdminInicialAsync(db, new SenhaService(), configuracao);

        Assert.Single(db.Usuarios);
    }

    [Fact]
    public async Task SemearAdminInicialAsync_SemSenhaConfigurada_NaoDeveCriarAdmin()
    {
        var db = CriarDbContext();
        var configuracao = new ConfigurationBuilder().Build();

        await DbSeeder.SemearAdminInicialAsync(db, new SenhaService(), configuracao);

        Assert.Empty(db.Usuarios);
    }

    [Fact]
    public async Task SemearAdminInicialAsync_ComEmailConfigurado_DeveUsarEsseEmail()
    {
        var db = CriarDbContext();
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_SENHA_INICIAL"] = "senhaForte123",
                ["ADMIN_EMAIL_INICIAL"] = "dono@clinica-real.com"
            })
            .Build();

        await DbSeeder.SemearAdminInicialAsync(db, new SenhaService(), configuracao);

        Assert.Equal("dono@clinica-real.com", db.Usuarios.Single().Email);
    }
}
