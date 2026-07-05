using Microsoft.EntityFrameworkCore;
using Triagem.API.Models;

namespace Triagem.API.Data;

/// <summary>
/// Cria o banco (se necessário) e popula as 6 triagens padrão do sistema.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(TriagemDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // Várias instâncias da API podem subir ao mesmo tempo (load balancer);
        // o applock do SQL Server garante que só uma execute o seed.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            await db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = 'TriarSeed', @LockMode = 'Exclusive', " +
                "@LockOwner = 'Transaction', @LockTimeout = 60000;");

            if (!await db.TriagemModelos.AnyAsync())
            {
                db.TriagemModelos.AddRange(CriarModelosPadrao());
                await db.SaveChangesAsync();
            }

            await tx.CommitAsync();
        });
    }

    private static List<TriagemModelo> CriarModelosPadrao()
    {

        var modelos = new List<TriagemModelo>
        {
            Modelo("Triagem em Saúde Mental", "Adolescentes e adultos", "🧠",
                "Avaliação inicial de sinais de ansiedade, depressão e estresse.",
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
            Modelo("Triagem em Saúde Infantil", "Crianças de 0 a 12 anos", "🧒",
                "Acompanhamento de sinais de alerta no desenvolvimento e saúde da criança.",
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
            Modelo("Triagem em Saúde da Mulher", "Mulheres de todas as idades", "👩",
                "Rastreio de sinais importantes para a saúde da mulher.",
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
            Modelo("Triagem em Saúde do Idoso", "Pessoas com 60 anos ou mais", "🧓",
                "Avaliação de riscos comuns na terceira idade: quedas, memória e autonomia.",
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
            Modelo("Triagem Respiratória", "Adolescentes e adultos", "🫁",
                "Identificação de sintomas respiratórios que merecem avaliação.",
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
            Modelo("Triagem Clínica Geral", "Todas as idades", "🩺",
                "Avaliação geral de sinais e sintomas para orientar a busca por atendimento.",
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
        };

        return modelos;
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
