using Triagem.Core.Domain;

namespace Triagem.Core.Tests;

public class TriagemRulesTests
{
    [Fact]
    public void RespostasUnicasECompletas_SaoValidas() =>
        Assert.Equal(RespostasStatus.Validas,
            TriagemRules.ValidarRespostas([1, 2, 3], [3, 1, 2]).Status);

    [Fact]
    public void PerguntaDesconhecida_TemPrecedenciaSobreCompletude()
    {
        var resultado = TriagemRules.ValidarRespostas([1, 2], [999]);
        Assert.Equal(RespostasStatus.PerguntaDesconhecida, resultado.Status);
        Assert.Equal(999, resultado.PerguntaDesconhecida);
    }

    [Fact]
    public void Duplicidade_ERejeitada() =>
        Assert.Equal(RespostasStatus.Duplicadas,
            TriagemRules.ValidarRespostas([1, 2], [1, 1]).Status);

    [Fact]
    public void RespostaFaltando_ERejeitada() =>
        Assert.Equal(RespostasStatus.Incompletas,
            TriagemRules.ValidarRespostas([1, 2], [1]).Status);

    [Theory]
    [InlineData("image/png", "89504E470D0A1A0A")]
    [InlineData("image/jpeg", "FFD8FF")]
    [InlineData("image/webp", "524946460000000057454250")]
    public void AssinaturasConhecidas_SaoAceitas(string mime, string hexadecimal) =>
        Assert.True(TriagemRules.AssinaturaImagemValida(mime, Convert.FromHexString(hexadecimal)));

    [Fact]
    public void ExtensaoSemAssinatura_ERejeitada() =>
        Assert.False(TriagemRules.AssinaturaImagemValida("image/png", "arquivo falso"u8));
}
