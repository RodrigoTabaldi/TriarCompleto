using Microsoft.EntityFrameworkCore;
using Triagem.API.Models;

namespace Triagem.API.Data;

public class TriagemDbContext(DbContextOptions<TriagemDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TriagemModelo> TriagemModelos => Set<TriagemModelo>();
    public DbSet<Pergunta> Perguntas => Set<Pergunta>();
    public DbSet<FaixaResultado> FaixasResultado => Set<FaixaResultado>();
    public DbSet<UsuarioTriagemHome> UsuarioTriagensHome => Set<UsuarioTriagemHome>();
    public DbSet<TriagemResultado> TriagemResultados => Set<TriagemResultado>();
    public DbSet<RespostaDada> RespostasDadas => Set<RespostaDada>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Nome).HasMaxLength(120);
            e.Property(u => u.Email).HasMaxLength(180);
            e.Property(u => u.SenhaHash).HasMaxLength(500);
        });

        mb.Entity<TriagemModelo>(e =>
        {
            e.ToTable("TriagemModelos");
            e.Property(t => t.Titulo).HasMaxLength(150);
            e.Property(t => t.PublicoAlvo).HasMaxLength(150);
            e.Property(t => t.Descricao).HasMaxLength(600);
            e.Property(t => t.Icone).HasMaxLength(16);
            e.HasOne(t => t.CriadorUsuario)
                .WithMany(u => u.TriagensCriadas)
                .HasForeignKey(t => t.CriadorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(t => t.Perguntas).WithOne(p => p.TriagemModelo)
                .HasForeignKey(p => p.TriagemModeloId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.Faixas).WithOne(f => f.TriagemModelo)
                .HasForeignKey(f => f.TriagemModeloId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Pergunta>(e =>
        {
            e.ToTable("Perguntas");
            e.Property(p => p.Texto).HasMaxLength(500);
        });

        mb.Entity<FaixaResultado>(e =>
        {
            e.ToTable("FaixasResultado");
            e.Property(f => f.Titulo).HasMaxLength(120);
            e.Property(f => f.Recomendacao).HasMaxLength(600);
            e.Property(f => f.Cor).HasMaxLength(9);
        });

        mb.Entity<UsuarioTriagemHome>(e =>
        {
            e.ToTable("UsuarioTriagensHome");
            e.HasKey(h => new { h.UsuarioId, h.TriagemModeloId });
            e.HasOne(h => h.Usuario).WithMany(u => u.ConfiguracaoHome)
                .HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.TriagemModelo).WithMany()
                .HasForeignKey(h => h.TriagemModeloId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<TriagemResultado>(e =>
        {
            e.ToTable("TriagemResultados");
            e.Property(r => r.NomePaciente).HasMaxLength(150);
            e.Property(r => r.Sexo).HasMaxLength(30);
            e.Property(r => r.Classificacao).HasMaxLength(120);
            e.Property(r => r.Recomendacao).HasMaxLength(600);
            e.Property(r => r.Cor).HasMaxLength(9);
            e.HasIndex(r => new { r.UsuarioId, r.TriagemModeloId });
            e.HasOne(r => r.TriagemModelo).WithMany()
                .HasForeignKey(r => r.TriagemModeloId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Usuario).WithMany()
                .HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Respostas).WithOne(x => x.TriagemResultado)
                .HasForeignKey(x => x.TriagemResultadoId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RespostaDada>(e => e.ToTable("RespostasDadas"));
    }
}
