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
}
