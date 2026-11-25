using System.Text.Json;
using Triagem.Core.Domain;

var models = DefaultTriageCatalog.Items.Select((item, i) =>
{
    var isFono = item.LegacyTitle == DefaultTriageCatalog.TituloOriginalFono;
    var title = isFono ? DefaultTriageCatalog.TituloPrincipalFono : item.Title;
    var max = item.Questions.Sum(p => p.Weight);
    var first = max / 3;
    var second = max * 2 / 3;
    var ranges = title == DefaultTriageCatalog.TituloRiscoVocal
        ? VocalTriageRules.Ranges.Select(f => new { titulo = f.Title, recomendacao = "", pontuacaoMin = f.Min, pontuacaoMax = f.Max, cor = f.Color }).ToArray()
        : new[] {
            new { titulo = "Baixo risco", recomendacao = "Sem sinais de alerta relevantes no momento. Mantenha hábitos saudáveis e acompanhamento de rotina.", pontuacaoMin = 0, pontuacaoMax = first, cor = "#10B981" },
            new { titulo = "Risco moderado", recomendacao = "Alguns sinais merecem atenção. Recomenda-se agendar uma avaliação com um profissional de saúde.", pontuacaoMin = first + 1, pontuacaoMax = second, cor = "#F59E0B" },
            new { titulo = "Alto risco", recomendacao = "Vários sinais de alerta identificados. Procure atendimento profissional o quanto antes.", pontuacaoMin = second + 1, pontuacaoMax = max, cor = "#EF4444" }
        };
    return new {
        id = i + 1, titulo = title, publicoAlvo = item.Audience, descricao = item.Description,
        icone = item.Icon, padrao = true, criadorUsuarioId = (int?)null,
        perguntas = item.Questions.Select((p, n) => new { id = (i + 1) * 100 + n + 1, texto = p.Options is { Count: > 0 } && !p.SingleChoice ? $"Engasga com algum dos seguintes: {string.Join(", ", p.Options)}?" : p.Text, peso = p.Weight, ordem = n + 1, categoria = p.Category ?? "", opcoes = p.Options ?? [], selecaoUnica = p.SingleChoice }),
        faixas = ranges
    };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true }));
