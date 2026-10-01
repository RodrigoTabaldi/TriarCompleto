using Microsoft.EntityFrameworkCore;
using Triagem.API.Models;
using Triagem.Core.Domain;
using System.Text.Json;

namespace Triagem.API.Data;

/// <summary>
/// Cria o banco (se necessário) e sincroniza o catálogo padrão do MAUI atualizado.
/// </summary>
public static class DbSeeder
{
    public static async Task<bool> SeedAsync(TriagemDbContext db)
    {
        var changed = false;
        await db.Database.EnsureCreatedAsync();
        await SchemaUpgrade.ApplyAsync(db);

        // Várias instâncias da API podem subir ao mesmo tempo (load balancer);
        // o advisory lock do PostgreSQL garante que só uma execute o seed.
        // O lock é liberado automaticamente ao final da transação (xact).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(917283);");

            foreach (var item in DefaultTriageCatalog.Items)
            {
                if (await db.TriagemModelos.AnyAsync(t => t.CriadorUsuarioId == null && t.Titulo == item.Title)) continue;
                if (item.LegacyTitle != item.Title)
                {
                    var legados = await db.TriagemModelos.Where(t => t.CriadorUsuarioId == null && t.Titulo == item.LegacyTitle).ToListAsync();
                    foreach (var legado in legados) legado.Ativa = false;
                }
                var modelo = Modelo(item.Title, item.Audience, item.Icon, item.Description,
                    item.Questions.Select(p => (p.Text, p.Weight * Math.Max(1, p.Options?.Count ?? 0))).ToArray());
                modelo.Perguntas = item.Questions.Select((p, n) => new Pergunta
                {
                    Texto = p.Text, Peso = p.Weight, Ordem = n + 1,
                    Categoria = p.Category ?? "", OpcoesJson = JsonSerializer.Serialize(p.Options ?? [])
                }).ToList();
                string[] titulos = ["Poucos sinais relatados", "Sinais que merecem avaliação", "Vários sinais relatados"];
                string[] recomendacoes = ["Foram relatados poucos sinais neste rastreio. Observe a evolução e mantenha o acompanhamento de rotina.", "Há sinais que merecem atenção. Considere agendar uma avaliação com profissional habilitado.", "Foram relatados vários sinais. Procure avaliação profissional; este resultado não é um diagnóstico."];
                for (var i = 0; i < modelo.Faixas.Count; i++) { modelo.Faixas[i].Titulo = titulos[i]; modelo.Faixas[i].Recomendacao = recomendacoes[i]; }
                db.TriagemModelos.Add(modelo);
                changed = true;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });
        return changed;
    }

    private static TriagemModelo Modelo(
        string titulo, string publico, string icone, string descricao,
        (string Texto, int Peso)[] perguntas)
    {
        var pesoTotal = perguntas.Sum(p => p.Peso);
        var corte1 = pesoTotal / 3;
        var corte2 = (pesoTotal * 2) / 3;

        return new TriagemModelo
        {
            Titulo = titulo,
            PublicoAlvo = publico,
            Icone = icone,
            Descricao = descricao,
            Perguntas = perguntas
                .Select((p, i) => new Pergunta { Texto = p.Texto, Peso = p.Peso, Ordem = i + 1 })
                .ToList(),
            Faixas =
            [
                new FaixaResultado
                {
                    Titulo = "Baixo risco", Ordem = 1,
                    PontuacaoMin = 0, PontuacaoMax = corte1,
                    Cor = "#10B981",
                    Recomendacao = "Sem sinais de alerta relevantes no momento. Mantenha hábitos saudáveis e acompanhamento de rotina."
                },
                new FaixaResultado
                {
                    Titulo = "Risco moderado", Ordem = 2,
                    PontuacaoMin = corte1 + 1, PontuacaoMax = corte2,
                    Cor = "#F59E0B",
                    Recomendacao = "Alguns sinais merecem atenção. Recomenda-se agendar uma avaliação com um profissional de saúde."
                },
                new FaixaResultado
                {
                    Titulo = "Alto risco", Ordem = 3,
                    PontuacaoMin = corte2 + 1, PontuacaoMax = pesoTotal,
                    Cor = "#EF4444",
                    Recomendacao = "Vários sinais de alerta identificados. Procure atendimento profissional o quanto antes."
                },
            ]
        };
    }
}
