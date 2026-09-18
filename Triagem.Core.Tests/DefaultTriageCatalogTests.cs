using Triagem.Core.Domain;

namespace Triagem.Core.Tests;

public class DefaultTriageCatalogTests
{
    [Fact]
    public void CatalogoDeclaraExplicitamenteQueAindaNaoFoiValidadoClinicamente()
    {
        Assert.False(DefaultTriageCatalog.ClinicallyValidated);
        Assert.Contains("não homologado", DefaultTriageCatalog.ValidationNotice);
    }
    [Fact]
    public void Catalogo_TemTriagensComPerguntasValidasEDistintas()
    {
        Assert.Equal(7, DefaultTriageCatalog.Items.Count);
        Assert.Equal(7, DefaultTriageCatalog.Items.Select(t => t.Title).Distinct().Count());

        foreach (var item in DefaultTriageCatalog.Items)
        {
            Assert.InRange(item.Questions.Count, 1, 50);
            Assert.All(item.Questions, p =>
            {
                Assert.False(string.IsNullOrWhiteSpace(p.Text));
                Assert.InRange(p.Weight, 1, 100);
            });
            Assert.Contains("Não substitui", item.Description, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ProtocoloIntegrado_EPrimeiroETemCategoriasENumeracaoCompleta()
    {
        var protocolo = DefaultTriageCatalog.Items[0];

        Assert.Equal("Protocolo de Triagem Fonoaudiológica Integrada", protocolo.Title);
        Assert.Equal(20, protocolo.Questions.Count);
        Assert.Equal(
            ["Deglutição", "Linguagem e Cognição", "Voz", "Audição e Equilíbrio"],
            protocolo.Questions.Select(p => p.Category!).Distinct().ToArray());
        Assert.Contains(protocolo.Questions, p => p.Text.StartsWith("18.", StringComparison.Ordinal));

        var multipla = protocolo.Questions.Single(p => p.Text.StartsWith("7.", StringComparison.Ordinal));
        Assert.Equal(1, multipla.Weight);
        Assert.Equal(["Líquidos", "Pastosos", "Sólidos", "Saliva"], multipla.Options);
        Assert.Equal(39, protocolo.Questions.Sum(p =>
            p.Weight * Math.Max(1, p.Options?.Count ?? 0)));
    }

    [Fact]
    public void Catalogo_NaoContemPerguntasClinicasAntigasIncompativeis()
    {
        var texto = string.Join(' ', DefaultTriageCatalog.Items
            .SelectMany(t => t.Questions)
            .Select(p => p.Text));

        Assert.DoesNotContain("ciclo menstrual", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pressão arterial", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pensamentos de se machucar", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("catarro com sangue", texto, StringComparison.OrdinalIgnoreCase);
    }
}
