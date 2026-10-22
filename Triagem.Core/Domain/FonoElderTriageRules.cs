namespace Triagem.Core.Domain;

public static class FonoElderTriageRules
{
    // Nestas perguntas, responder "Não" indica o sinal de alerta.
    public static int Score(int numero, int peso, bool respostaSim) =>
        (numero is 10 or 11 or 12 or 13 or 15 or 20 ? !respostaSim : respostaSim) ? peso : 0;

    public static bool ConsistenciasValidas(bool respostaSim, IReadOnlyCollection<string> permitidas,
        IReadOnlyList<string>? selecionadas)
    {
        selecionadas ??= [];
        return respostaSim
            ? selecionadas.Count > 0 && selecionadas.Count == selecionadas.Distinct().Count() &&
              selecionadas.All(permitidas.Contains)
            : selecionadas.Count == 0;
    }
}
