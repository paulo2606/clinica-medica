using AgendamentoClinica.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendamentoClinica.Api.Data;

public class AgendamentoDbContext : DbContext
{
    public AgendamentoDbContext(DbContextOptions<AgendamentoDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TokenRenovacao> TokensRenovacao => Set<TokenRenovacao>();
    public DbSet<Especialidade> Especialidades => Set<Especialidade>();
    public DbSet<Medico> Medicos => Set<Medico>();
    public DbSet<HorarioTrabalhoMedico> HorariosTrabalhoMedico => Set<HorarioTrabalhoMedico>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Consulta> Consultas => Set<Consulta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entidade =>
        {
            entidade.ToTable("usuarios");
            entidade.HasKey(u => u.Id);
            entidade.Property(u => u.Id).HasColumnName("id");
            entidade.Property(u => u.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
            entidade.Property(u => u.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            entidade.HasIndex(u => u.Email).IsUnique();
            entidade.Property(u => u.SenhaHash).HasColumnName("senha_hash").IsRequired();
            entidade.Property(u => u.Papel).HasColumnName("papel").HasConversion<string>().HasMaxLength(20);
            entidade.Property(u => u.Ativo).HasColumnName("ativo");
            entidade.Property(u => u.CriadoEm).HasColumnName("criado_em");
        });

        modelBuilder.Entity<TokenRenovacao>(entidade =>
        {
            entidade.ToTable("tokens_renovacao");
            entidade.HasKey(t => t.Id);
            entidade.Property(t => t.Id).HasColumnName("id");
            entidade.Property(t => t.UsuarioId).HasColumnName("usuario_id");
            entidade.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
            entidade.HasIndex(t => t.TokenHash).IsUnique();
            entidade.Property(t => t.ExpiraEm).HasColumnName("expira_em");
            entidade.Property(t => t.RevogadoEm).HasColumnName("revogado_em");
            entidade.Property(t => t.CriadoEm).HasColumnName("criado_em");
            entidade.HasOne(t => t.Usuario)
                .WithMany()
                .HasForeignKey(t => t.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Especialidade>(entidade =>
        {
            entidade.ToTable("especialidades");
            entidade.HasKey(e => e.Id);
            entidade.Property(e => e.Id).HasColumnName("id");
            entidade.Property(e => e.Nome).HasColumnName("nome").HasMaxLength(100).IsRequired();
            entidade.HasIndex(e => e.Nome).IsUnique();
        });

        modelBuilder.Entity<Medico>(entidade =>
        {
            entidade.ToTable("medicos");
            entidade.HasKey(m => m.Id);
            entidade.Property(m => m.Id).HasColumnName("id");
            entidade.Property(m => m.UsuarioId).HasColumnName("usuario_id");
            entidade.Property(m => m.EspecialidadeId).HasColumnName("especialidade_id");
            entidade.Property(m => m.Crm).HasColumnName("crm").HasMaxLength(20).IsRequired();
            entidade.HasIndex(m => m.Crm).IsUnique();
            entidade.Property(m => m.DuracaoConsultaPadraoMinutos).HasColumnName("duracao_consulta_padrao_minutos");

            entidade.HasOne(m => m.Usuario)
                .WithMany()
                .HasForeignKey(m => m.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
            entidade.HasIndex(m => m.UsuarioId).IsUnique();

            entidade.HasOne(m => m.Especialidade)
                .WithMany()
                .HasForeignKey(m => m.EspecialidadeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HorarioTrabalhoMedico>(entidade =>
        {
            entidade.ToTable("horarios_trabalho_medico");
            entidade.HasKey(h => h.Id);
            entidade.Property(h => h.Id).HasColumnName("id");
            entidade.Property(h => h.MedicoId).HasColumnName("medico_id");
            entidade.Property(h => h.DiaSemana).HasColumnName("dia_semana").HasConversion<string>().HasMaxLength(15);
            entidade.Property(h => h.HoraInicio).HasColumnName("hora_inicio");
            entidade.Property(h => h.HoraFim).HasColumnName("hora_fim");

            entidade.HasOne(h => h.Medico)
                .WithMany()
                .HasForeignKey(h => h.MedicoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Paciente>(entidade =>
        {
            entidade.ToTable("pacientes");
            entidade.HasKey(p => p.Id);
            entidade.Property(p => p.Id).HasColumnName("id");
            entidade.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
            entidade.Property(p => p.Cpf).HasColumnName("cpf").HasMaxLength(14).IsRequired();
            entidade.HasIndex(p => p.Cpf).IsUnique();
            entidade.Property(p => p.Telefone).HasColumnName("telefone").HasMaxLength(20).IsRequired();
            entidade.Property(p => p.Email).HasColumnName("email").HasMaxLength(200);
            entidade.Property(p => p.DataNascimento).HasColumnName("data_nascimento");
            entidade.Property(p => p.CriadoEm).HasColumnName("criado_em");
        });

        modelBuilder.Entity<Consulta>(entidade =>
        {
            entidade.ToTable("consultas");
            entidade.HasKey(c => c.Id);
            entidade.Property(c => c.Id).HasColumnName("id");
            entidade.Property(c => c.PacienteId).HasColumnName("paciente_id");
            entidade.Property(c => c.MedicoId).HasColumnName("medico_id");
            entidade.Property(c => c.DataHora).HasColumnName("data_hora");
            entidade.Property(c => c.DuracaoMinutos).HasColumnName("duracao_minutos");
            entidade.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            entidade.Property(c => c.Observacoes).HasColumnName("observacoes");
            entidade.Property(c => c.CriadoPorUsuarioId).HasColumnName("criado_por_usuario_id");
            entidade.Property(c => c.CriadoEm).HasColumnName("criado_em");
            entidade.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");

            entidade.HasOne(c => c.Paciente)
                .WithMany()
                .HasForeignKey(c => c.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entidade.HasOne(c => c.Medico)
                .WithMany()
                .HasForeignKey(c => c.MedicoId)
                .OnDelete(DeleteBehavior.Restrict);

            entidade.HasOne(c => c.CriadoPorUsuario)
                .WithMany()
                .HasForeignKey(c => c.CriadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
