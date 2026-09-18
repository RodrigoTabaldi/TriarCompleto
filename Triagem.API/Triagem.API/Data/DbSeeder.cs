using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Triagem.API.Models;
using Triagem.API.Services;
using Triagem.Core.Domain;

namespace Triagem.API.Data;

/// <summary>
/// Cria o banco (se necessário) e popula as 6 triagens padrão do sistema.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(TriagemDbContext db, FieldEncryptionService encryptor)
    {
        await PrepararSchemaAsync(db);
        await SeedDataAsync(db, encryptor);
    }

    public static async Task SeedDataAsync(TriagemDbContext db, FieldEncryptionService encryptor)
    {
        await MigrarDadosClinicosLegadosAsync(db, encryptor);
        await PreservarQuestionariosHistoricosAsync(db, encryptor);

        // Várias instâncias da API podem subir ao mesmo tempo (load balancer);
        // o applock do SQL Server garante que só uma execute o seed.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            await db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = 'TriarSeed', @LockMode = 'Exclusive', " +
                "@LockOwner = 'Transaction', @LockTimeout = 60000;");

            await SincronizarModelosPadraoAsync(db);

            await tx.CommitAsync();
        });
    }

    private static async Task PreservarQuestionariosHistoricosAsync(
        TriagemDbContext db, FieldEncryptionService encryptor)
    {
        const int tamanhoLote = 200;
        var ultimoId = 0;

        while (true)
        {
            var resultados = await db.TriagemResultados
                .Include(r => r.TriagemModelo)!.ThenInclude(t => t!.Perguntas)
                .Include(r => r.Respostas)
                .Where(r => r.Id > ultimoId && r.DadosProtegidos != null)
                .OrderBy(r => r.Id)
                .Take(tamanhoLote)
                .ToListAsync();
            if (resultados.Count == 0) break;

            foreach (var resultado in resultados)
            {
                ultimoId = resultado.Id;
                var json = JsonNode.Parse(encryptor.Decrypt(resultado.DadosProtegidos)) as JsonObject;
                if (json is null || json["Questionario"] is JsonArray { Count: > 0 }) continue;

                var perguntas = resultado.TriagemModelo?.Perguntas.ToDictionary(p => p.Id) ?? [];
                var questionario = new JsonArray();
                foreach (var resposta in resultado.Respostas.OrderBy(r => r.Id))
                {
                    if (!perguntas.TryGetValue(resposta.PerguntaId, out var pergunta)) continue;
                    var valor = resposta.ValorProtegido is not null
                        ? encryptor.Decrypt(resposta.ValorProtegido) == "1"
                        : resposta.Valor;
                    questionario.Add(new JsonObject
                    {
                        ["Pergunta"] = pergunta.Texto,
                        ["Peso"] = pergunta.Peso,
                        ["Valor"] = valor
                    });
                }

                json["TituloTriagem"] = resultado.TriagemModelo?.Titulo ?? "Triagem";
                json["Questionario"] = questionario;
                resultado.DadosProtegidos = encryptor.Encrypt(json.ToJsonString());
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    private static async Task SincronizarModelosPadraoAsync(TriagemDbContext db)
    {
        foreach (var item in DefaultTriageCatalog.Items)
        {
            var modelo = await db.TriagemModelos
                .Include(t => t.Perguntas)
                .Include(t => t.Faixas)
                .FirstOrDefaultAsync(t => t.CriadorUsuarioId == null &&
                    (t.Titulo == item.LegacyTitle || t.Titulo == item.Title));

            if (modelo is null)
            {
                db.TriagemModelos.Add(CriarModeloPadrao(item));
                continue;
            }

            modelo.Titulo = item.Title;
            modelo.PublicoAlvo = item.Audience;
            modelo.Icone = item.Icon;
            modelo.Descricao = item.Description;
            modelo.Ativa = true;

            var perguntasAtuais = modelo.Perguntas.OrderBy(p => p.Ordem).ToList();
            var faixasEsperadas = CriarFaixas(PontuacaoMaxima(item));
            var faixasAtuais = modelo.Faixas.OrderBy(f => f.Ordem).ToList();
            var perguntasMudaram = perguntasAtuais.Count != item.Questions.Count ||
                perguntasAtuais.Where((p, i) =>
                    p.Texto != item.Questions[i].Text ||
                    p.Peso != item.Questions[i].Weight ||
                    p.Categoria != (item.Questions[i].Category ?? "") ||
                    p.OpcoesJson != SerializarOpcoes(item.Questions[i].Options)).Any();
            var faixasMudaram = faixasAtuais.Count != faixasEsperadas.Count ||
                faixasAtuais.Where((f, i) =>
                    f.Titulo != faixasEsperadas[i].Titulo ||
                    f.Recomendacao != faixasEsperadas[i].Recomendacao ||
                    f.PontuacaoMin != faixasEsperadas[i].PontuacaoMin ||
                    f.PontuacaoMax != faixasEsperadas[i].PontuacaoMax ||
                    f.Cor != faixasEsperadas[i].Cor).Any();

            if (perguntasMudaram || faixasMudaram)
            {
                db.Perguntas.RemoveRange(modelo.Perguntas);
                db.FaixasResultado.RemoveRange(modelo.Faixas);
                modelo.Perguntas = CriarPerguntas(item);
                modelo.Faixas = faixasEsperadas;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task MigrarDadosClinicosLegadosAsync(
        TriagemDbContext db, FieldEncryptionService encryptor)
    {
        const int tamanhoLote = 200;

        while (true)
        {
            var resultados = await db.TriagemResultados
                .Where(r => r.DadosProtegidos == null)
                .OrderBy(r => r.Id)
                .Take(tamanhoLote)
                .ToListAsync();
            if (resultados.Count == 0) break;

            foreach (var r in resultados)
            {
                var dados = new
                {
                    r.NomePaciente,
                    r.Idade,
                    r.Sexo,
                    r.Pontuacao,
                    r.PontuacaoMaxima,
                    r.Classificacao,
                    r.Recomendacao,
                    r.Cor
                };
                r.DadosProtegidos = encryptor.Encrypt(JsonSerializer.Serialize(dados));
                r.NomePaciente = "";
                r.Idade = 0;
                r.Sexo = "";
                r.Pontuacao = 0;
                r.PontuacaoMaxima = 0;
                r.Classificacao = "";
                r.Recomendacao = "";
                r.Cor = "#000000";
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        while (true)
        {
            var respostas = await db.RespostasDadas
                .Where(r => r.ValorProtegido == null)
                .OrderBy(r => r.Id)
                .Take(tamanhoLote)
                .ToListAsync();
            if (respostas.Count == 0) break;

            foreach (var r in respostas)
            {
                r.ValorProtegido = encryptor.Encrypt(r.Valor ? "1" : "0");
                r.Valor = false;
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    public static async Task PrepararSchemaAsync(TriagemDbContext db)
    {
        if (!await db.Database.CanConnectAsync())
        {
            await db.Database.MigrateAsync();
            return;
        }

        var conexao = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var bootstrapTx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = 'TriarSchemaBootstrap', @LockMode = 'Exclusive', " +
                "@LockOwner = 'Transaction', @LockTimeout = 60000;");

            await using var comando = conexao.CreateCommand();
            comando.Transaction = bootstrapTx.GetDbTransaction();
            comando.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL THEN 0 ELSE 1 END";
            var esquemaLegado = Convert.ToInt32(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;

            if (esquemaLegado)
            {
                // Bancos de versões anteriores foram criados por EnsureCreated. Depois
                // de completar a última coluna conhecida, registramos o baseline para
                // que todas as próximas mudanças sejam migrations EF normais.
                await db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH('dbo.TriagemModelos', 'Imagem') IS NULL
                        ALTER TABLE dbo.TriagemModelos ADD Imagem NVARCHAR(MAX) NULL;

                    IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
                    BEGIN
                        CREATE TABLE [__EFMigrationsHistory] (
                            [MigrationId] nvarchar(150) NOT NULL,
                            [ProductVersion] nvarchar(32) NOT NULL,
                            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                        );
                    END;

                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260824185226_InitialSchema')
                        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                        VALUES ('20260824185226_InitialSchema', '10.0.0');
                    """);
            }

            await bootstrapTx.CommitAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        await db.Database.MigrateAsync();
    }

    private static TriagemModelo CriarModeloPadrao(DefaultTriage item)
    {
        var pesoTotal = PontuacaoMaxima(item);

        return new TriagemModelo
        {
            Titulo = item.Title,
            PublicoAlvo = item.Audience,
            Icone = item.Icon,
            Descricao = item.Description,
            Perguntas = CriarPerguntas(item),
            Faixas = CriarFaixas(pesoTotal),
        };
    }

    private static List<Pergunta> CriarPerguntas(DefaultTriage item) =>
        item.Questions
            .Select((p, i) => new Pergunta
            {
                Texto = p.Text,
                Peso = p.Weight,
                Categoria = p.Category ?? "",
                OpcoesJson = SerializarOpcoes(p.Options),
                Ordem = i + 1
            })
            .ToList();

    private static int PontuacaoMaxima(DefaultTriage item) =>
        item.Questions.Sum(p => p.Weight * Math.Max(1, p.Options?.Count ?? 0));

    private static string? SerializarOpcoes(IReadOnlyList<string>? opcoes) =>
        opcoes is { Count: > 0 } ? JsonSerializer.Serialize(opcoes) : null;

    private static List<FaixaResultado> CriarFaixas(int pesoTotal)
    {
        var corte1 = pesoTotal / 3;
        var corte2 = (pesoTotal * 2) / 3;

        return
        [
            new FaixaResultado
            {
                Titulo = "Poucos sinais relatados", Ordem = 1,
                PontuacaoMin = 0, PontuacaoMax = corte1,
                Cor = "#10B981",
                Recomendacao = "Foram relatados poucos sinais neste rastreio. Observe a evolução e mantenha o acompanhamento de rotina."
            },
            new FaixaResultado
            {
                Titulo = "Sinais que merecem avaliação", Ordem = 2,
                PontuacaoMin = corte1 + 1, PontuacaoMax = corte2,
                Cor = "#F59E0B",
                Recomendacao = "Há sinais que merecem atenção. Considere agendar uma avaliação com profissional habilitado."
            },
            new FaixaResultado
            {
                Titulo = "Vários sinais relatados", Ordem = 3,
                PontuacaoMin = corte2 + 1, PontuacaoMax = pesoTotal,
                Cor = "#EF4444",
                Recomendacao = "Foram relatados vários sinais. Procure avaliação profissional; este resultado não é um diagnóstico."
            },
        ];
    }
}
