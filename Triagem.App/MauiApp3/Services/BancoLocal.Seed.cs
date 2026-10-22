using System.Text.Json;
using SQLite;
using Triagem.Core.Domain;

namespace MauiApp3.Services;

/// <summary>
/// Carga inicial do banco local: as triagens fonoaudiológicas do catálogo e a
/// conta de demonstração com um histórico de exemplo,
/// para que o app já abra com conteúdo em vez de telas vazias.
/// </summary>
public static partial class BancoLocal
{
    private static async Task SemearAsync(SQLiteAsyncConnection db)
    {
        await SemearTriagensEspecificasAsync(db);

        await SemearTriagensPrincipaisAsync(db);

        if (await db.Table<UsuarioLocal>().CountAsync() == 0)
            await SemearContaDemoAsync(db);
    }

    private static async Task SemearTriagensPrincipaisAsync(SQLiteAsyncConnection db)
    {
        foreach (var item in DefaultTriageCatalog.TriagensPrincipais)
        {
            var titulo = item.Title == DefaultTriageCatalog.TituloRiscoVocal
                ? DefaultTriageCatalog.TituloRiscoVocal : DefaultTriageCatalog.TituloPrincipalFono;
            var existente = await db.Table<TriagemModeloLocal>().Where(t => t.Titulo == titulo).FirstOrDefaultAsync();
            if (existente is not null)
            {
                if (titulo == DefaultTriageCatalog.TituloRiscoVocal)
                    await AtualizarRiscoVocalAsync(db, existente.Id, item);
                else
                    await AtualizarFonoIdosoAsync(db, existente.Id, item);
                continue;
            }

            var modelo = new TriagemModeloLocal
            {
                Titulo = titulo, PublicoAlvo = item.Audience, Icone = item.Icon,
                Descricao = item.Description, CriadorUsuarioId = null, Ativa = true
            };
            await db.InsertAsync(modelo);
            await db.InsertAllAsync(item.Questions.Select((q, i) => new PerguntaLocal
            {
                TriagemModeloId = modelo.Id, Ordem = i + 1, Peso = q.Weight,
                Texto = q.Options is { Count: > 0 } && !q.SingleChoice
                    ? $"Engasga com algum dos seguintes: {string.Join(", ", q.Options)}?" : q.Text,
                Categoria = q.Category ?? "",
                OpcoesJson = JsonSerializer.Serialize(q.Options ?? []),
                SelecaoUnica = q.SingleChoice
            }).ToList());
            await db.InsertAllAsync(titulo == DefaultTriageCatalog.TituloRiscoVocal
                ? FaixasRiscoVocal(modelo.Id) : FaixasPadrao(modelo.Id, item.Questions.Sum(q => q.Weight)));
        }
    }

    private static async Task AtualizarFonoIdosoAsync(SQLiteAsyncConnection db, int modeloId, DefaultTriage item)
    {
        var perguntas = await db.Table<PerguntaLocal>().Where(p => p.TriagemModeloId == modeloId).ToListAsync();
        if (perguntas.Count != item.Questions.Count)
            throw new InvalidOperationException("A triagem fonoaudiológica existente não tem 20 perguntas; atualize-a sem excluir o histórico.");

        foreach (var pergunta in perguntas)
        {
            var origem = item.Questions[pergunta.Ordem - 1];
            pergunta.Texto = origem.Options is { Count: > 0 } && !origem.SingleChoice
                ? $"Engasga com algum dos seguintes: {string.Join(", ", origem.Options)}?" : origem.Text;
            pergunta.Peso = origem.Weight;
            pergunta.Categoria = origem.Category ?? "";
            pergunta.OpcoesJson = JsonSerializer.Serialize(origem.Options ?? []);
            pergunta.SelecaoUnica = origem.SingleChoice;
        }
        await db.UpdateAllAsync(perguntas);

        var faixas = await db.Table<FaixaLocal>().Where(f => f.TriagemModeloId == modeloId).ToListAsync();
        foreach (var faixa in faixas)
            await db.DeleteAsync(faixa);
        await db.InsertAllAsync(FaixasPadrao(modeloId, item.Questions.Sum(q => q.Weight)));
    }

    private static async Task AtualizarRiscoVocalAsync(SQLiteAsyncConnection db, int modeloId, DefaultTriage item)
    {
        var perguntas = await db.Table<PerguntaLocal>().Where(p => p.TriagemModeloId == modeloId).ToListAsync();
        if (perguntas.Count == item.Questions.Count)
        {
            foreach (var pergunta in perguntas)
            {
                var origem = item.Questions[pergunta.Ordem - 1];
                pergunta.Texto = origem.Text;
                pergunta.Peso = origem.Weight;
                pergunta.Categoria = origem.Category ?? "";
                pergunta.OpcoesJson = origem.SingleChoice ? JsonSerializer.Serialize(origem.Options) : "[]";
                pergunta.SelecaoUnica = origem.SingleChoice;
            }
            await db.UpdateAllAsync(perguntas);
        }

        var faixas = await db.Table<FaixaLocal>().Where(f => f.TriagemModeloId == modeloId).ToListAsync();
        foreach (var faixa in faixas)
            await db.DeleteAsync(faixa);
        await db.InsertAllAsync(FaixasRiscoVocal(modeloId));
    }

    private static List<FaixaLocal> FaixasRiscoVocal(int modeloId) =>
        VocalTriageRules.Ranges.Select((f, i) => new FaixaLocal
        {
            TriagemModeloId = modeloId, Titulo = f.Title, PontuacaoMin = f.Min,
            PontuacaoMax = f.Max, Cor = f.Color, Ordem = i + 1
        }).ToList();

    // ---------------- Triagens específicas de Fonoaudiologia ----------------

    private static async Task SemearTriagensEspecificasAsync(SQLiteAsyncConnection db)
    {
        var especificas = DefaultTriageCatalog.TriagensEspecificas.ToList();
        var titulosLegados = especificas.Where(t => t.LegacyTitle != t.Title)
            .Select(t => t.LegacyTitle).ToHashSet(StringComparer.Ordinal);
        var modelosPadrao = await db.Table<TriagemModeloLocal>()
            .Where(t => t.CriadorUsuarioId == null).ToListAsync();

        foreach (var legado in modelosPadrao.Where(t => t.Ativa && titulosLegados.Contains(t.Titulo)))
        {
            legado.Ativa = false;
            await db.UpdateAsync(legado);
        }

        foreach (var item in especificas)
        {
            var existente = modelosPadrao.FirstOrDefault(t => t.Titulo == item.Title);
            if (existente is not null)
            {
                if (!existente.Ativa)
                {
                    existente.Ativa = true;
                    await db.UpdateAsync(existente);
                }
                continue;
            }

            var modelo = new TriagemModeloLocal
            {
                Titulo = item.Title,
                PublicoAlvo = item.Audience,
                Icone = item.Icon,
                Descricao = item.Description,
                CriadorUsuarioId = null,
                Ativa = true
            };
            await db.InsertAsync(modelo);

            await db.InsertAllAsync(item.Questions
                .Select((p, i) => new PerguntaLocal
                {
                    TriagemModeloId = modelo.Id,
                    Texto = p.Text,
                    Peso = p.Weight,
                    Ordem = i + 1,
                    Categoria = p.Category ?? "",
                    OpcoesJson = JsonSerializer.Serialize(p.Options ?? []),
                    SelecaoUnica = p.SingleChoice
                }).ToList());

            await db.InsertAllAsync(FaixasPadrao(modelo.Id, item.Questions.Sum(p => p.Weight)));
        }
    }

    /// <summary>
    /// Faixas de resultado derivadas do peso total: baixo risco até 1/3 da pontuação,
    /// risco moderado até 2/3 e alto risco daí em diante — mesma regra do seed da API.
    /// </summary>
    private static List<FaixaLocal> FaixasPadrao(int modeloId, int pesoTotal)
    {
        var corte1 = pesoTotal / 3;
        var corte2 = pesoTotal * 2 / 3;

        return
        [
            new FaixaLocal
            {
                TriagemModeloId = modeloId, Titulo = "Baixo risco", Ordem = 1,
                PontuacaoMin = 0, PontuacaoMax = corte1, Cor = "#10B981",
                Recomendacao = "Sem sinais de alerta relevantes no momento. Mantenha hábitos saudáveis e acompanhamento de rotina."
            },
            new FaixaLocal
            {
                TriagemModeloId = modeloId, Titulo = "Risco moderado", Ordem = 2,
                PontuacaoMin = corte1 + 1, PontuacaoMax = corte2, Cor = "#F59E0B",
                Recomendacao = "Alguns sinais merecem atenção. Recomenda-se agendar uma avaliação com um profissional de saúde."
            },
            new FaixaLocal
            {
                TriagemModeloId = modeloId, Titulo = "Alto risco", Ordem = 3,
                PontuacaoMin = corte2 + 1, PontuacaoMax = pesoTotal, Cor = "#EF4444",
                Recomendacao = "Vários sinais de alerta identificados. Procure atendimento profissional o quanto antes."
            },
        ];
    }

    // ---------------- Conta e histórico de demonstração ----------------

    private static async Task SemearContaDemoAsync(SQLiteAsyncConnection db)
    {
        var demo = new UsuarioLocal
        {
            Nome = "Usuário Demonstração",
            Email = EmailDemo,
            SenhaHash = HashSenha(SenhaDemo)
        };
        await db.InsertAsync(demo);

        // Histórico fictício, só para a tela de histórico e a exportação em Excel
        // já terem o que mostrar numa demonstração.
        var exemplos = new (string Triagem, string Nome, int Idade, string Sexo, int Pontuacao, int DiasAtras)[]
        {
            ("Triagem em Saúde Mental", "Ana Paula Ribeiro", 34, "Feminino", 4, 1),
            ("Triagem em Saúde do Idoso", "Carlos Eduardo Menezes", 71, "Masculino", 11, 2),
            ("Triagem em Saúde Infantil", "Beatriz Nogueira", 7, "Feminino", 2, 4),
            ("Triagem Respiratória", "Marcos Vinícius Alves", 52, "Masculino", 8, 6),
            ("Triagem em Saúde da Mulher", "Helena Duarte", 29, "Feminino", 6, 9),
            ("Triagem Clínica Geral", "Roberto Lima", 45, "Masculino", 3, 13),
        };

        var modelos = await db.Table<TriagemModeloLocal>().ToListAsync();

        foreach (var e in exemplos)
        {
            var modelo = modelos.FirstOrDefault(m => m.Titulo == e.Triagem);
            if (modelo is null) continue;

            var pesoTotal = (await db.Table<PerguntaLocal>().Where(p => p.TriagemModeloId == modelo.Id).ToListAsync())
                .Sum(p => p.Peso);

            var faixas = (await db.Table<FaixaLocal>().Where(f => f.TriagemModeloId == modelo.Id).ToListAsync())
                .OrderBy(f => f.Ordem).ToList();

            var faixa = faixas.FirstOrDefault(f => e.Pontuacao >= f.PontuacaoMin && e.Pontuacao <= f.PontuacaoMax)
                        ?? faixas.LastOrDefault();

            var dadosSensiveis = new ResultadoSensivelLocal
            {
                NomePaciente = e.Nome,
                Idade = e.Idade,
                Sexo = e.Sexo,
                Pontuacao = e.Pontuacao,
                PontuacaoMaxima = pesoTotal,
                Classificacao = faixa?.Titulo ?? "Sem classificação",
                Recomendacao = faixa?.Recomendacao ?? "",
                Cor = faixa?.Cor ?? "#10B981"
            };

            await db.InsertAsync(new ResultadoLocal
            {
                TriagemModeloId = modelo.Id,
                UsuarioId = demo.Id,
                DadosProtegidos = LocalDataProtection.Proteger(JsonSerializer.Serialize(dadosSensiveis, JsonOptions)),
                Data = DateTime.UtcNow.AddDays(-e.DiasAtras)
            });
        }
    }
}
