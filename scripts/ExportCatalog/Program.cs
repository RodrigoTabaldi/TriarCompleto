using System.Text.Json;
using Triagem.Core.Domain;

var models = DefaultTriageCatalog.Items.Select((item, i) =>
{
    var max = item.Questions.Sum(p => p.Weight * Math.Max(1, p.Options?.Count ?? 0));
    var first = max / 3;
    var second = max * 2 / 3;
    return new
    {
        id = i + 1, titulo = item.Title, publicoAlvo = item.Audience, descricao = item.Description,
        icone = item.Icon, padrao = true, criadorUsuarioId = (int?)null,
        perguntas = item.Questions.Select((p, n) => new { id = (i + 1) * 100 + n + 1, texto = p.Text, peso = p.Weight, ordem = n + 1, categoria = p.Category ?? "", opcoes = p.Options ?? [] }),
        faixas = new[]
        {
            new { titulo = "Poucos sinais relatados", recomendacao = "Foram relatados poucos sinais neste rastreio. Observe a evolução e mantenha o acompanhamento de rotina.", pontuacaoMin = 0, pontuacaoMax = first, cor = "#10B981" },
            new { titulo = "Sinais que merecem avaliação", recomendacao = "Há sinais que merecem atenção. Considere agendar uma avaliação com profissional habilitado.", pontuacaoMin = first + 1, pontuacaoMax = second, cor = "#F59E0B" },
            new { titulo = "Vários sinais relatados", recomendacao = "Foram relatados vários sinais. Procure avaliação profissional; este resultado não é um diagnóstico.", pontuacaoMin = second + 1, pontuacaoMax = max, cor = "#EF4444" },
        }
    };
});
var json = JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(args[0], json);
