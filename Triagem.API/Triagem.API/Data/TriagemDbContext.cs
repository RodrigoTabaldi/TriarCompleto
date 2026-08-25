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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Nome).HasMaxLength(120);
            e.Property(u => u.Email).HasMaxLength(180);
            e.Property(u => u.SenhaHash).HasMaxLength(500);
        });

        modelBuilder.Entity<TriagemModelo>(e =>
        {
            e.ToTable("TriagemModelos");
            e.Property(t => t.Titulo).HasMaxLength(150);
            e.Property(t => t.PublicoAlvo).HasMaxLength(150);
            e.Property(t => t.Descricao).HasMaxLength(600);
            e.Property(t => t.Icone).HasMaxLength(16);
            e.Property(t => t.Imagem).HasColumnType("nvarchar(max)");
            e.HasOne(t => t.CriadorUsuario)
                .WithMany(u => u.TriagensCriadas)
                .HasForeignKey(t => t.CriadorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(t => t.Perguntas).WithOne(p => p.TriagemModelo)
                .HasForeignKey(p => p.TriagemModeloId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.Faixas).WithOne(f => f.TriagemModelo)
                .HasForeignKey(f => f.TriagemModeloId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Pergunta>(e =>
        {
            e.ToTable("Perguntas");
            e.Property(p => p.Texto).HasMaxLength(500);
        });

        modelBuilder.Entity<FaixaResultado>(e =>
        {
            e.ToTable("FaixasResultado");
            e.Property(f => f.Titulo).HasMaxLength(120);
            e.Property(f => f.Recomendacao).HasMaxLength(600);
            e.Property(f => f.Cor).HasMaxLength(9);
        });

        modelBuilder.Entity<UsuarioTriagemHome>(e =>
        {
            e.ToTable("UsuarioTriagensHome");
            e.HasKey(h => new { h.UsuarioId, h.TriagemModeloId });
            e.HasOne(h => h.Usuario).WithMany(u => u.ConfiguracaoHome)
                .HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.TriagemModelo).WithMany()
                .HasForeignKey(h => h.TriagemModeloId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TriagemResultado>(e =>
        {
            e.ToTable("TriagemResultados");
            // NomePaciente não guarda mais o nome: os campos clínicos completos (nome
            // incluído) vivem só no envelope DadosProtegidos (AES-256-GCM), gravado em
            // TriagemService/BancoLocal. A coluna permanece vazia daqui em diante — é
            // mantida (em vez de removida) só para servir de fallback de leitura a
            // registros anteriores à migração ProtectClinicalData, que ainda podem ter
            // o nome em texto puro aqui até o próximo start da API rodar o backfill em
            // DbSeeder.MigrarDadosClinicosLegadosAsync. Por isso nenhuma criptografia é
            // aplicada aqui: encriptar uma string sempre vazia a cada escrita nova seria
            // custo puro sem ganho de segurança.
            e.Property(r => r.NomePaciente).HasMaxLength(500);
            e.Property(r => r.Sexo).HasMaxLength(30);
            e.Property(r => r.Classificacao).HasMaxLength(120);
            e.Property(r => r.Recomendacao).HasMaxLength(600);
            e.Property(r => r.Cor).HasMaxLength(9);
            e.Property(r => r.DadosProtegidos).HasColumnType("nvarchar(max)");
            e.HasIndex(r => new { r.UsuarioId, r.TriagemModeloId, r.Data });
            e.HasOne(r => r.TriagemModelo).WithMany()
                .HasForeignKey(r => r.TriagemModeloId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Usuario).WithMany()
                .HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Respostas).WithOne(x => x.TriagemResultado)
                .HasForeignKey(x => x.TriagemResultadoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RespostaDada>(e =>
        {
            e.ToTable("RespostasDadas");
            e.Property(r => r.ValorProtegido).HasMaxLength(200);
        });
    }
}
