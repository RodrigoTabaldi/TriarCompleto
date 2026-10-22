using Triagem.Core.Domain;

namespace Triagem.Core.Tests;

public class DefaultTriageCatalogTests
{
    [Fact]
    public void TriagensPrincipais_ContemExatamenteOsDoisModelosSolicitados()
    {
        var principais = DefaultTriageCatalog.TriagensPrincipais.ToList();

        Assert.Equal(2, principais.Count);
        Assert.Contains(principais, item => item.Title == DefaultTriageCatalog.TituloRiscoVocal);
        Assert.Contains(principais, item => item.LegacyTitle == DefaultTriageCatalog.TituloOriginalFono);
        Assert.All(principais, item => Assert.NotEmpty(item.Questions));
        Assert.Contains(principais.Single(item => item.Title == DefaultTriageCatalog.TituloRiscoVocal).Questions,
            pergunta => pergunta.SingleChoice && pergunta.Options is { Count: > 0 });
    }

    [Fact]
    public void FonoIdoso_TemVintePerguntasNaOrdemEPesosSolicitados()
    {
        var perguntas = DefaultTriageCatalog.TriagensPrincipais
            .Single(item => item.LegacyTitle == DefaultTriageCatalog.TituloOriginalFono).Questions;

        Assert.Equal(20, perguntas.Count);
        Assert.Equal([2, 3, 3, 2, 2, 2, 2, 3, 2, 3, 2, 2, 3, 2, 1, 2, 1, 2, 2, 3],
            perguntas.Select(pergunta => pergunta.Weight).ToArray());
        Assert.Equal(["Líquidos", "Pastosos", "Sólidos", "Saliva"], perguntas[7].Options);
        Assert.False(perguntas[7].SingleChoice);
        Assert.Equal("Deglutição", perguntas[7].Category);
        Assert.Equal("Audição e Equilíbrio", perguntas[19].Category);
    }

    [Theory]
    [InlineData(1, true, 2)]
    [InlineData(1, false, 0)]
    [InlineData(10, true, 0)]
    [InlineData(10, false, 3)]
    [InlineData(15, true, 0)]
    [InlineData(15, false, 1)]
    [InlineData(20, true, 0)]
    [InlineData(20, false, 3)]
    public void FonoIdoso_PontuaSinaisDeRisco(int numero, bool respostaSim, int esperado)
    {
        var peso = DefaultTriageCatalog.TriagensPrincipais
            .Single(item => item.LegacyTitle == DefaultTriageCatalog.TituloOriginalFono).Questions[numero - 1].Weight;
        Assert.Equal(esperado, FonoElderTriageRules.Score(numero, peso, respostaSim));
    }

    [Fact]
    public void FonoIdoso_ValidaAsConsistenciasDaPerguntaOito()
    {
        var permitidas = new[] { "Líquidos", "Pastosos", "Sólidos", "Saliva" };

        Assert.True(FonoElderTriageRules.ConsistenciasValidas(true, permitidas, ["Líquidos", "Saliva"]));
        Assert.True(FonoElderTriageRules.ConsistenciasValidas(false, permitidas, []));
        Assert.False(FonoElderTriageRules.ConsistenciasValidas(true, permitidas, []));
        Assert.False(FonoElderTriageRules.ConsistenciasValidas(true, permitidas, ["Líquidos", "Líquidos"]));
        Assert.False(FonoElderTriageRules.ConsistenciasValidas(true, permitidas, ["Outro"]));
        Assert.False(FonoElderTriageRules.ConsistenciasValidas(false, permitidas, ["Saliva"]));
    }
}
