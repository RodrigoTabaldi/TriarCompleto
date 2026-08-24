namespace Triagem.Core.Domain;

public enum RespostasStatus
{
    Validas,
    PerguntaDesconhecida,
    Incompletas,
    Duplicadas
}

public readonly record struct RespostasValidation(RespostasStatus Status, int? PerguntaDesconhecida = null);

public static class TriagemRules
{
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
}
