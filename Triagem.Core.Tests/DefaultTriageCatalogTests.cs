using Triagem.Core.Domain;

namespace Triagem.Core.Tests;

public class DefaultTriageCatalogTests
{
    [Fact]
    public void Catalogo_TemSeisTriagensComPerguntasValidasEDistintas()
    {
        Assert.Equal(6, DefaultTriageCatalog.Items.Count);
        Assert.Equal(6, DefaultTriageCatalog.Items.Select(t => t.Title).Distinct().Count());

        foreach (var item in DefaultTriageCatalog.Items)
        {
            Assert.Equal(10, item.Questions.Count);
            Assert.All(item.Questions, p =>
            {
                Assert.False(string.IsNullOrWhiteSpace(p.Text));
                Assert.InRange(p.Weight, 1, 100);
            });
            Assert.Contains("Não substitui", item.Description, StringComparison.OrdinalIgnoreCase);
        }
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
