using System.Text.RegularExpressions;

namespace Triagem.Core.Domain;

public enum RespostasStatus
{
    Validas,
    PerguntaDesconhecida,
    Incompletas,
    Duplicadas
}

public readonly record struct RespostasValidation(RespostasStatus Status, int? PerguntaDesconhecida = null);

/// <summary>Uma pergunta de entrada (texto + peso), independente de como o chamador a representa (DTO da API ou payload do app).</summary>
public readonly record struct PerguntaEntrada(string Texto, int Peso);

/// <summary>Uma faixa de resultado de entrada, independente de como o chamador a representa.</summary>
public readonly record struct FaixaEntrada(string Titulo, string? Recomendacao, int PontuacaoMin, int PontuacaoMax, string? Cor);

/// <summary>
/// Regras de negócio compartilhadas entre a Triagem.API e o modo offline do app
/// (MauiApp3.Services.BancoLocal) — validação de modelo de triagem, validação de
/// respostas e de imagens de capa. Existir aqui, em vez de duplicada nos dois lados,
/// é o que garante que API e app nunca divirjam sobre o que é uma triagem válida.
/// </summary>
public static partial class TriagemRules
{
    public const int TamanhoMaximoImagemBytes = 2 * 1024 * 1024;
    private const int TamanhoMaximoBase64Imagem = ((TamanhoMaximoImagemBytes + 2) / 3) * 4;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex CorHexRegex();
    private static readonly Regex CorHexValida = CorHexRegex();

    public static RespostasValidation ValidarRespostas(
        IReadOnlyCollection<int> perguntasEsperadas,
        IReadOnlyCollection<int>? perguntasRespondidas)
    {
        if (perguntasRespondidas is null)
            return new(RespostasStatus.Incompletas);

        var esperadas = perguntasEsperadas.ToHashSet();
        foreach (var id in perguntasRespondidas)
            if (!esperadas.Contains(id))
                return new(RespostasStatus.PerguntaDesconhecida, id);

        if (perguntasRespondidas.Count != esperadas.Count)
            return new(RespostasStatus.Incompletas);
        if (perguntasRespondidas.Distinct().Count() != perguntasRespondidas.Count)
            return new(RespostasStatus.Duplicadas);

        return new(RespostasStatus.Validas);
    }

    /// <summary>Mensagem de erro em português correspondente a uma validação de respostas mal-sucedida, ou null se válida.</summary>
    public static string? MensagemErroRespostas(RespostasValidation validacao) => validacao.Status switch
    {
        RespostasStatus.PerguntaDesconhecida => $"Pergunta {validacao.PerguntaDesconhecida} não pertence a esta triagem.",
        RespostasStatus.Incompletas => "Responda todas as perguntas da triagem uma única vez.",
        RespostasStatus.Duplicadas => "Cada pergunta deve ser respondida uma única vez.",
        _ => null
    };

    public static bool AssinaturaImagemValida(string mime, ReadOnlySpan<byte> bytes)
    {
        if (mime.Contains("png", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (mime.Contains("jpeg", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        return bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8);
    }

    /// <summary>
    /// Validação de um modelo de triagem: título, perguntas (com pesos) e faixas de
    /// resultado (sem sobreposição, cobrindo de 0 até a pontuação máxima). Retorna a
    /// mensagem de erro em português, ou null se o modelo é válido.
    /// </summary>
    public static string? ValidarModelo(string titulo, IReadOnlyList<PerguntaEntrada>? perguntas, IReadOnlyList<FaixaEntrada>? faixas)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return "Informe o título da triagem.";
        if (titulo.Trim().Length > 150) return "O título deve ter no máximo 150 caracteres.";
        if (perguntas is null || perguntas.Count == 0) return "Adicione pelo menos uma pergunta.";
        if (perguntas.Count > 50) return "Máximo de 50 perguntas por triagem.";
        if (perguntas.Any(p => string.IsNullOrWhiteSpace(p.Texto))) return "Toda pergunta precisa de um texto.";
        if (perguntas.Any(p => p.Texto.Trim().Length > 500)) return "Cada pergunta deve ter no máximo 500 caracteres.";
        if (perguntas.Any(p => p.Peso is < 1 or > 100)) return "O peso de cada pergunta deve estar entre 1 e 100.";
        if (faixas is null || faixas.Count < 2) return "Defina pelo menos duas faixas de resultado.";
        if (faixas.Count > 100) return "Máximo de 100 faixas de resultado por triagem.";
        if (faixas.Any(f => string.IsNullOrWhiteSpace(f.Titulo))) return "Toda faixa de resultado precisa de um título.";
        if (faixas.Any(f => f.Titulo.Trim().Length > 120)) return "O título de cada faixa deve ter no máximo 120 caracteres.";
        if (faixas.Any(f => (f.Recomendacao?.Trim().Length ?? 0) > 600)) return "A recomendação deve ter no máximo 600 caracteres.";
        if (faixas.Any(f => f.PontuacaoMin > f.PontuacaoMax)) return "Em cada faixa, a pontuação mínima deve ser menor ou igual à máxima.";
        if (faixas.Any(f => !string.IsNullOrWhiteSpace(f.Cor) && !CorHexValida.IsMatch(f.Cor)))
            return "A cor de cada faixa deve ser um hexadecimal válido (ex.: #10B981).";

        var ordenadas = faixas.OrderBy(f => f.PontuacaoMin).ToList();
        for (var i = 1; i < ordenadas.Count; i++)
        {
            if (ordenadas[i].PontuacaoMin <= ordenadas[i - 1].PontuacaoMax)
                return "As faixas de resultado não podem se sobrepor.";
        }

        var pesoTotal = perguntas.Sum(p => p.Peso);
        if (ordenadas[0].PontuacaoMin > 0)
            return "A primeira faixa deve começar em 0.";
        if (ordenadas[^1].PontuacaoMax < pesoTotal)
            return $"A última faixa deve cobrir até a pontuação máxima ({pesoTotal}).";

        return null;
    }

    /// <summary>Valida uma imagem em data URL base64: formato declarado, tamanho e assinatura binária real.</summary>
    public static string? ValidarImagemBase64(string? imagem)
    {
        if (string.IsNullOrWhiteSpace(imagem)) return null;

        var valor = imagem.Trim();
        var formatos = new[] { "data:image/png;base64,", "data:image/jpeg;base64,", "data:image/webp;base64," };
        var prefixo = formatos.FirstOrDefault(p => valor.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (prefixo is null) return "A imagem deve estar no formato PNG, JPG ou WebP.";

        var base64 = valor[prefixo.Length..];
        if (base64.Length > TamanhoMaximoBase64Imagem)
            return "A imagem deve ter no máximo 2 MB.";

        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length > TamanhoMaximoImagemBytes)
                return "A imagem deve ter no máximo 2 MB.";

            if (!AssinaturaImagemValida(prefixo, bytes))
                return "O conteúdo do arquivo não corresponde ao formato de imagem informado.";
        }
        catch (FormatException)
        {
            return "Os dados da imagem são inválidos.";
        }

        return null;
    }

    /// <summary>Remove espaços das pontas de uma imagem em data URL, ou null se vazia.</summary>
    public static string? NormalizarImagem(string? imagem) =>
        string.IsNullOrWhiteSpace(imagem) ? null : imagem.Trim();

    /// <summary>Cor padrão (verde/âmbar/vermelho) para a i-ésima faixa quando o chamador não informa uma.</summary>
    public static string CorPadrao(int indice) => indice switch
    {
        0 => "#10B981",
        1 => "#F59E0B",
        _ => "#EF4444",
    };
}
