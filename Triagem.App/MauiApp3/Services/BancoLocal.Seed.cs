using System.Text.Json;
using SQLite;
using Triagem.Core.Domain;

namespace MauiApp3.Services;

/// <summary>Sincroniza o catálogo padrão compartilhado e cria a conta de demonstração.</summary>
public static partial class BancoLocal
{
    private static async Task SemearAsync(SQLiteAsyncConnection db)
    {
        await SincronizarTriagensPadraoAsync(db);
        if (await db.Table<UsuarioLocal>().CountAsync() == 0)
            await SemearContaDemoAsync(db);
    }

    private static async Task SincronizarTriagensPadraoAsync(SQLiteAsyncConnection db)
    {
        var modelos = await db.Table<TriagemModeloLocal>().ToListAsync();
        foreach (var item in DefaultTriageCatalog.Items)
        {
            var modelo = modelos.FirstOrDefault(t => t.CriadorUsuarioId == null &&
                (t.Titulo == item.LegacyTitle || t.Titulo == item.Title));

            if (modelo is null)
            {
                modelo = new TriagemModeloLocal
                {
                    Titulo = item.Title,
                    PublicoAlvo = item.Audience,
                    Icone = item.Icon,
                    Descricao = item.Description,
                    CriadorUsuarioId = null,
                    Ativa = true
                };
                await db.InsertAsync(modelo);
                await GravarConteudoPadraoAsync(db, modelo.Id, item);
                modelos.Add(modelo);
                continue;
            }

            modelo.Titulo = item.Title;
            modelo.PublicoAlvo = item.Audience;
            modelo.Icone = item.Icon;
            modelo.Descricao = item.Description;
            modelo.Ativa = true;

            var atuais = (await db.Table<PerguntaLocal>()
                    .Where(p => p.TriagemModeloId == modelo.Id)
                    .ToListAsync())
                .OrderBy(p => p.Ordem)
                .ToList();
            var perguntasMudaram = atuais.Count != item.Questions.Count ||
                atuais.Where((p, i) =>
                    p.Texto != item.Questions[i].Text || p.Peso != item.Questions[i].Weight).Any();
            var faixasEsperadas = FaixasPadrao(modelo.Id, item.Questions.Sum(p => p.Weight));
            var faixasAtuais = (await db.Table<FaixaLocal>()
                    .Where(f => f.TriagemModeloId == modelo.Id)
                    .ToListAsync())
                .OrderBy(f => f.Ordem)
                .ToList();
            var faixasMudaram = faixasAtuais.Count != faixasEsperadas.Count ||
                faixasAtuais.Where((f, i) =>
                    f.Titulo != faixasEsperadas[i].Titulo ||
                    f.Recomendacao != faixasEsperadas[i].Recomendacao ||
                    f.PontuacaoMin != faixasEsperadas[i].PontuacaoMin ||
                    f.PontuacaoMax != faixasEsperadas[i].PontuacaoMax ||
                    f.Cor != faixasEsperadas[i].Cor).Any();

            await db.RunInTransactionAsync(conn =>
            {
                conn.Update(modelo);
                if (!perguntasMudaram && !faixasMudaram) return;

                conn.Execute("DELETE FROM perguntas WHERE TriagemModeloId = ?", modelo.Id);
                conn.Execute("DELETE FROM faixas WHERE TriagemModeloId = ?", modelo.Id);
                conn.InsertAll(CriarPerguntasPadrao(modelo.Id, item));
                conn.InsertAll(faixasEsperadas);
            });
        }
    }

    private static async Task GravarConteudoPadraoAsync(
        SQLiteAsyncConnection db, int modeloId, DefaultTriage item)
    {
        await db.InsertAllAsync(CriarPerguntasPadrao(modeloId, item));
        await db.InsertAllAsync(FaixasPadrao(modeloId, item.Questions.Sum(p => p.Weight)));
    }

    private static List<PerguntaLocal> CriarPerguntasPadrao(int modeloId, DefaultTriage item) =>
        item.Questions.Select((p, i) => new PerguntaLocal
        {
            TriagemModeloId = modeloId,
            Texto = p.Text,
            Peso = p.Weight,
            Ordem = i + 1
        }).ToList();

    private static List<FaixaLocal> FaixasPadrao(int modeloId, int pesoTotal)
    {
        var corte1 = pesoTotal / 3;
        var corte2 = pesoTotal * 2 / 3;
        return
        [
            new FaixaLocal
            {
                TriagemModeloId = modeloId, Titulo = "Poucos sinais relatados", Ordem = 1,
                PontuacaoMin = 0, PontuacaoMax = corte1, Cor = "#10B981",
                Recomendacao = "Foram relatados poucos sinais neste rastreio. Observe a evolução e mantenha o acompanhamento de rotina."
            },
            new FaixaLocal
            {
                TriagemModeloId = modeloId, Titulo = "Sinais que merecem avaliação", Ordem = 2,
                PontuacaoMin = corte1 + 1, PontuacaoMax = corte2, Cor = "#F59E0B",
                Recomendacao = "Há sinais que merecem atenção. Considere agendar uma avaliação com profissional habilitado."
            },
            new FaixaLocal
            {
                TriagemModeloId = modeloId, Titulo = "Vários sinais relatados", Ordem = 3,
                PontuacaoMin = corte2 + 1, PontuacaoMax = pesoTotal, Cor = "#EF4444",
                Recomendacao = "Foram relatados vários sinais. Procure avaliação profissional; este resultado não é um diagnóstico."
            },
        ];
    }

    private static async Task SemearContaDemoAsync(SQLiteAsyncConnection db)
    {
        var demo = new UsuarioLocal
        {
            Nome = "Usuário Demonstração",
            Email = EmailDemo,
            SenhaHash = await Task.Run(() => HashSenha(SenhaDemo))
        };
        await db.InsertAsync(demo);

        var exemplos = new (string Triagem, string Nome, int Idade, string Sexo, int Pontuacao, int DiasAtras)[]
        {
            ("Triagem de Linguagem e Cognição", "Ana Paula Ribeiro", 34, "Feminino", 4, 1),
            ("Triagem Auditiva do Idoso", "Carlos Eduardo Menezes", 71, "Masculino", 11, 2),
            ("Triagem Fonoaudiológica Infantil", "Beatriz Nogueira", 7, "Feminino", 2, 4),
            ("Triagem de Voz", "Marcos Vinícius Alves", 52, "Masculino", 8, 6),
            ("Triagem de Motricidade Orofacial", "Helena Duarte", 29, "Feminino", 6, 9),
            ("Triagem Auditiva", "Roberto Lima", 45, "Masculino", 3, 13),
        };

        var modelos = await db.Table<TriagemModeloLocal>().ToListAsync();
        foreach (var exemplo in exemplos)
        {
            var modelo = modelos.FirstOrDefault(m => m.Titulo == exemplo.Triagem);
            if (modelo is null) continue;

            var pesoTotal = (await db.Table<PerguntaLocal>()
                    .Where(p => p.TriagemModeloId == modelo.Id)
                    .ToListAsync())
                .Sum(p => p.Peso);
            var faixas = (await db.Table<FaixaLocal>()
                    .Where(f => f.TriagemModeloId == modelo.Id)
                    .ToListAsync())
                .OrderBy(f => f.Ordem)
                .ToList();
            var faixa = faixas.FirstOrDefault(f =>
                exemplo.Pontuacao >= f.PontuacaoMin && exemplo.Pontuacao <= f.PontuacaoMax) ?? faixas.Last();
            var dadosSensiveis = new ResultadoSensivelLocal
            {
                NomePaciente = exemplo.Nome,
                Idade = exemplo.Idade,
                Sexo = exemplo.Sexo,
                Pontuacao = exemplo.Pontuacao,
                PontuacaoMaxima = pesoTotal,
                Classificacao = faixa.Titulo,
                Recomendacao = faixa.Recomendacao,
                Cor = faixa.Cor
            };

            await db.InsertAsync(new ResultadoLocal
            {
                TriagemModeloId = modelo.Id,
                UsuarioId = demo.Id,
                DadosProtegidos = LocalDataProtection.Proteger(
                    JsonSerializer.Serialize(dadosSensiveis, JsonOptions)),
                Data = DateTime.UtcNow.AddDays(-exemplo.DiasAtras)
            });
        }
    }
}
