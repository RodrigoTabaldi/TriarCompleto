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

    // ---------------- ValidarModelo (compartilhada entre API e BancoLocal) ----------------

    private static readonly List<PerguntaEntrada> PerguntasValidas =
        [new PerguntaEntrada("Pergunta 1?", 2), new PerguntaEntrada("Pergunta 2?", 3)];

    private static readonly List<FaixaEntrada> FaixasValidas =
        [new FaixaEntrada("Baixo risco", "ok", 0, 2, null), new FaixaEntrada("Alto risco", "procure ajuda", 3, 5, null)];

    [Fact]
    public void ValidarModelo_ComDadosValidos_RetornaNull() =>
        Assert.Null(TriagemRules.ValidarModelo("Triagem de Teste", PerguntasValidas, FaixasValidas));

    [Fact]
    public void ValidarModelo_TituloVazio_RetornaErro() =>
        Assert.Equal("Informe o título da triagem.",
            TriagemRules.ValidarModelo("", PerguntasValidas, FaixasValidas));

    [Fact]
    public void ValidarModelo_SemPerguntas_RetornaErro() =>
        Assert.Equal("Adicione pelo menos uma pergunta.",
            TriagemRules.ValidarModelo("Triagem", [], FaixasValidas));

    [Fact]
    public void ValidarModelo_MenosDeDuasFaixas_RetornaErro() =>
        Assert.Equal("Defina pelo menos duas faixas de resultado.",
            TriagemRules.ValidarModelo("Triagem", PerguntasValidas, [FaixasValidas[0]]));

    [Fact]
    public void ValidarModelo_PesoForaDoIntervalo_RetornaErro() =>
        Assert.Equal("O peso de cada pergunta deve estar entre 1 e 100.",
            TriagemRules.ValidarModelo("Triagem", [new PerguntaEntrada("P?", 0)], FaixasValidas));

    [Fact]
    public void ValidarModelo_FaixasSobrepostas_RetornaErro()
    {
        var faixas = new List<FaixaEntrada> { new("A", null, 0, 3, null), new("B", null, 2, 5, null) };
        Assert.Equal("As faixas de resultado não podem se sobrepor.",
            TriagemRules.ValidarModelo("Triagem", PerguntasValidas, faixas));
    }

    [Fact]
    public void ValidarModelo_PrimeiraFaixaNaoComecaEmZero_RetornaErro()
    {
        var faixas = new List<FaixaEntrada> { new("A", null, 1, 2, null), new("B", null, 3, 5, null) };
        Assert.Equal("A primeira faixa deve começar em 0.",
            TriagemRules.ValidarModelo("Triagem", PerguntasValidas, faixas));
    }

    [Fact]
    public void ValidarModelo_CorInvalida_RetornaErro()
    {
        var faixas = new List<FaixaEntrada> { new("A", null, 0, 2, "vermelho"), new("B", null, 3, 5, null) };
        Assert.Equal("A cor de cada faixa deve ser um hexadecimal válido (ex.: #10B981).",
            TriagemRules.ValidarModelo("Triagem", PerguntasValidas, faixas));
    }

    // ---------------- ValidarImagemBase64 ----------------

    [Fact]
    public void ValidarImagemBase64_Vazia_RetornaNull() =>
        Assert.Null(TriagemRules.ValidarImagemBase64(null));

    [Fact]
    public void ValidarImagemBase64_FormatoNaoSuportado_RetornaErro() =>
        Assert.Equal("A imagem deve estar no formato PNG, JPG ou WebP.",
            TriagemRules.ValidarImagemBase64("data:image/gif;base64,AAAA"));

    [Fact]
    public void ValidarImagemBase64_AssinaturaIncompativel_RetornaErro()
    {
        var base64 = Convert.ToBase64String("não é png"u8.ToArray());
        Assert.Equal("O conteúdo do arquivo não corresponde ao formato de imagem informado.",
            TriagemRules.ValidarImagemBase64($"data:image/png;base64,{base64}"));
    }

    [Fact]
    public void ValidarImagemBase64_PngValido_RetornaNull()
    {
        const string pngUmPixel = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
        Assert.Null(TriagemRules.ValidarImagemBase64(pngUmPixel));
    }

    // ---------------- MensagemErroRespostas / CorPadrao ----------------

    [Fact]
    public void MensagemErroRespostas_MapeiaCadaStatusParaTextoEmPortugues()
    {
        Assert.Null(TriagemRules.MensagemErroRespostas(new RespostasValidation(RespostasStatus.Validas)));
        Assert.Equal("Cada pergunta deve ser respondida uma única vez.",
            TriagemRules.MensagemErroRespostas(new RespostasValidation(RespostasStatus.Duplicadas)));
        Assert.Equal("Responda todas as perguntas da triagem uma única vez.",
            TriagemRules.MensagemErroRespostas(new RespostasValidation(RespostasStatus.Incompletas)));
        Assert.Equal("Pergunta 42 não pertence a esta triagem.",
            TriagemRules.MensagemErroRespostas(new RespostasValidation(RespostasStatus.PerguntaDesconhecida, 42)));
    }

    [Theory]
    [InlineData(0, "#10B981")]
    [InlineData(1, "#F59E0B")]
    [InlineData(2, "#EF4444")]
    [InlineData(99, "#EF4444")]
    public void CorPadrao_SeguePaletaVerdeAmbarVermelho(int indice, string corEsperada) =>
        Assert.Equal(corEsperada, TriagemRules.CorPadrao(indice));
}
