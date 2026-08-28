using System.Text.Json;
using SQLite;

namespace MauiApp3.Services;

/// <summary>
/// Carga inicial do banco local: as 6 triagens padrão do sistema (as mesmas do
/// <c>DbSeeder</c> da API) e a conta de demonstração com um histórico de exemplo,
/// para que o app já abra com conteúdo em vez de telas vazias.
/// </summary>
public static partial class BancoLocal
{
    private static async Task SemearAsync(SQLiteAsyncConnection db)
    {
        if (await db.Table<TriagemModeloLocal>().CountAsync() == 0)
            await SemearTriagensPadraoAsync(db);

        await AtualizarModelosPadraoFonoAsync(db);

        if (await db.Table<UsuarioLocal>().CountAsync() == 0)
            await SemearContaDemoAsync(db);
    }

    // ---------------- Triagens padrão ----------------

    private static async Task SemearTriagensPadraoAsync(SQLiteAsyncConnection db)
    {
        foreach (var (titulo, publico, icone, descricao, perguntas) in TriagensPadrao())
        {
            var modelo = new TriagemModeloLocal
            {
                Titulo = titulo,
                PublicoAlvo = publico,
                Icone = icone,
                Descricao = descricao,
                CriadorUsuarioId = null,
                Ativa = true
            };
            await db.InsertAsync(modelo);

            await db.InsertAllAsync(perguntas
                .Select((p, i) => new PerguntaLocal
                {
                    TriagemModeloId = modelo.Id,
                    Texto = p.Texto,
                    Peso = p.Peso,
                    Ordem = i + 1
                }).ToList());

            await db.InsertAllAsync(FaixasPadrao(modelo.Id, perguntas.Sum(p => p.Peso)));
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

    private static List<(string Titulo, string Publico, string Icone, string Descricao, (string Texto, int Peso)[] Perguntas)> TriagensPadrao() =>
    [
        ("Triagem de Linguagem e Cognição", "Adultos e idosos", "🧠",
            "Rastreio inicial de linguagem, comunicação funcional e aspectos cognitivos relacionados.",
            [
                ("Nas últimas duas semanas, sentiu-se triste, desanimado(a) ou sem esperança?", 2),
                ("Perdeu o interesse ou prazer em atividades que antes gostava?", 2),
                ("Tem tido dificuldade para dormir ou tem dormido demais?", 1),
                ("Sente-se cansado(a) ou sem energia com frequência?", 1),
                ("Tem se sentido nervoso(a), ansioso(a) ou muito preocupado(a)?", 2),
                ("Tem dificuldade para se concentrar em tarefas do dia a dia?", 1),
                ("Sente-se agitado(a) ou irritado(a) com facilidade?", 1),
                ("Tem evitado contato com amigos ou familiares?", 1),
                ("Já teve pensamentos de se machucar ou de que seria melhor não existir?", 3),
                ("Sente que o estresse tem afetado seu trabalho ou estudos?", 1),
            ]),
        ("Triagem Fonoaudiológica Infantil", "Crianças de 0 a 12 anos", "🧒",
            "Acompanhamento de sinais de fala, linguagem, audição e comunicação na infância.",
            [
                ("A criança teve febre alta (acima de 38,5°C) nos últimos dias?", 2),
                ("Apresenta tosse persistente ou dificuldade para respirar?", 2),
                ("Tem recusado alimentação ou líquidos?", 2),
                ("Apresenta vômitos ou diarreia frequentes?", 2),
                ("Está mais sonolenta ou irritada que o normal?", 1),
                ("A vacinação está atrasada?", 1),
                ("Houve perda de peso ou dificuldade para ganhar peso?", 1),
                ("Apresenta manchas na pele ou palidez?", 1),
                ("Tem dificuldades de fala ou de interação esperadas para a idade?", 1),
                ("Dorme mal ou apresenta agitação constante à noite?", 1),
            ]),
        ("Triagem de Motricidade Orofacial", "Todas as idades", "👩",
            "Rastreio de sinais relacionados a mastigação, deglutição, respiração oral e musculatura orofacial.",
            [
                ("Sente dores pélvicas frequentes ou intensas?", 2),
                ("Notou alterações no ciclo menstrual nos últimos meses?", 1),
                ("Percebeu nódulos, secreção ou alterações nas mamas?", 3),
                ("Tem sangramentos fora do período menstrual?", 2),
                ("Está com exames preventivos (Papanicolau) atrasados?", 1),
                ("Sente dor ou desconforto nas relações íntimas?", 1),
                ("Apresenta sintomas urinários como ardência ou urgência?", 1),
                ("Tem histórico familiar de câncer de mama ou colo do útero?", 1),
                ("Está gestante ou suspeita de gravidez sem acompanhamento?", 2),
                ("Sente ondas de calor, insônia ou alterações de humor intensas?", 1),
            ]),
        ("Triagem Auditiva do Idoso", "Pessoas com 60 anos ou mais", "🧓",
            "Avaliação inicial de sinais de perda auditiva e impacto funcional na comunicação do idoso.",
            [
                ("Sofreu alguma queda nos últimos seis meses?", 2),
                ("Tem dificuldade para caminhar ou manter o equilíbrio?", 2),
                ("Esquece com frequência compromissos ou onde guardou objetos?", 2),
                ("Toma cinco ou mais medicamentos por dia?", 1),
                ("Perdeu peso sem intenção nos últimos meses?", 2),
                ("Tem dificuldade para enxergar ou ouvir mesmo com correção?", 1),
                ("Sente-se sozinho(a) ou desanimado(a) na maior parte do tempo?", 1),
                ("Precisa de ajuda para atividades básicas como banho ou vestir-se?", 2),
                ("Tem incontinência urinária que atrapalha o dia a dia?", 1),
                ("Deixou de sair de casa ou de fazer atividades que gostava?", 1),
            ]),
        ("Triagem de Voz", "Profissionais da voz e adultos", "🫁",
            "Identificação de sinais vocais como rouquidão, esforço, fadiga e alterações persistentes da voz.",
            [
                ("Tem tosse há mais de três semanas?", 2),
                ("Sente falta de ar ao realizar esforços leves?", 2),
                ("Apresenta chiado ou aperto no peito?", 2),
                ("Teve febre nos últimos dias acompanhada de sintomas respiratórios?", 1),
                ("Tem produção de catarro com sangue?", 3),
                ("É fumante ou convive com fumantes?", 1),
                ("Acorda à noite com crises de tosse ou falta de ar?", 2),
                ("Teve contato com alguém com tuberculose ou infecção respiratória?", 1),
                ("Sente dor no peito ao respirar fundo?", 1),
                ("Percebeu piora dos sintomas nas últimas semanas?", 1),
            ]),
        ("Triagem Auditiva", "Todas as idades", "🩺",
            "Rastreio inicial de dificuldades auditivas e necessidade de avaliação fonoaudiológica.",
            [
                ("Sente dores frequentes que não melhoram com repouso?", 2),
                ("Teve febre recorrente na última semana?", 2),
                ("Perdeu peso sem motivo aparente?", 2),
                ("Sente cansaço excessivo mesmo após descansar?", 1),
                ("Notou alterações na pressão arterial ou glicemia?", 2),
                ("Tem dores de cabeça fortes ou frequentes?", 1),
                ("Apresenta inchaço nas pernas ou no rosto?", 1),
                ("Percebeu alterações no intestino ou na urina?", 1),
                ("Está com consultas ou exames de rotina atrasados?", 1),
                ("Tem alguma dor ou sintoma que o(a) preocupa há mais de um mês?", 1),
            ]),
    ];

    // ---------------- Conta e histórico de demonstração ----------------

    private static async Task AtualizarModelosPadraoFonoAsync(SQLiteAsyncConnection db)
    {
        var ajustes = new (string Antigo, string Novo, string Publico, string Descricao)[]
        {
            ("Triagem em Saúde Mental", "Triagem de Linguagem e Cognição", "Adultos e idosos",
                "Rastreio inicial de linguagem, comunicação funcional e aspectos cognitivos relacionados."),
            ("Triagem em Saúde Infantil", "Triagem Fonoaudiológica Infantil", "Crianças de 0 a 12 anos",
                "Acompanhamento de sinais de fala, linguagem, audição e comunicação na infância."),
            ("Triagem em Saúde da Mulher", "Triagem de Motricidade Orofacial", "Todas as idades",
                "Rastreio de sinais relacionados a mastigação, deglutição, respiração oral e musculatura orofacial."),
            ("Triagem em Saúde do Idoso", "Triagem Auditiva do Idoso", "Pessoas com 60 anos ou mais",
                "Avaliação inicial de sinais de perda auditiva e impacto funcional na comunicação do idoso."),
            ("Triagem Respiratória", "Triagem de Voz", "Profissionais da voz e adultos",
                "Identificação de sinais vocais como rouquidão, esforço, fadiga e alterações persistentes da voz."),
            ("Triagem Clínica Geral", "Triagem Auditiva", "Todas as idades",
                "Rastreio inicial de dificuldades auditivas e necessidade de avaliação fonoaudiológica."),
        };

        var modelos = await db.Table<TriagemModeloLocal>().ToListAsync();
        foreach (var ajuste in ajustes)
        {
            var modelo = modelos.FirstOrDefault(t => t.Titulo == ajuste.Antigo || t.Titulo == ajuste.Novo);
            if (modelo is null) continue;

            modelo.Titulo = ajuste.Novo;
            modelo.PublicoAlvo = ajuste.Publico;
            modelo.Descricao = ajuste.Descricao;
            await db.UpdateAsync(modelo);
        }
    }

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
            ("Triagem de Linguagem e Cognição", "Ana Paula Ribeiro", 34, "Feminino", 4, 1),
            ("Triagem Auditiva do Idoso", "Carlos Eduardo Menezes", 71, "Masculino", 11, 2),
            ("Triagem Fonoaudiológica Infantil", "Beatriz Nogueira", 7, "Feminino", 2, 4),
            ("Triagem de Voz", "Marcos Vinícius Alves", 52, "Masculino", 8, 6),
            ("Triagem de Motricidade Orofacial", "Helena Duarte", 29, "Feminino", 6, 9),
            ("Triagem Auditiva", "Roberto Lima", 45, "Masculino", 3, 13),
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
