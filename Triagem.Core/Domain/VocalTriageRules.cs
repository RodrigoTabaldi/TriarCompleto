namespace Triagem.Core.Domain;

public static class VocalTriageRules
{
    public const int MaximumScore = 56;

    public static int Score(int questionOrder, int weight, bool yes, string? selectedOption)
    {
        if (questionOrder == 1) return 0;
        if (questionOrder == 22)
            return selectedOption is "2–4 horas por dia" or "4–6 horas por dia" ? 1 : 0;

        var riskAnswer = questionOrder is >= 15 and <= 17 ? !yes : yes;
        return riskAnswer ? weight : 0;
    }

    public static IReadOnlyList<(string Title, int Min, int Max, string Color)> Ranges { get; } =
    [
        ("Sem alteração significativa / risco baixo", 0, 13, "#10B981"),
        ("Alteração leve", 14, 27, "#F59E0B"),
        ("Alteração moderada", 28, 41, "#F97316"),
        ("Alteração severa", 42, 56, "#EF4444")
    ];
}
