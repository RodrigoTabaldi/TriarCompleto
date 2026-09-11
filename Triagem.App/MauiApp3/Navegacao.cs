namespace MauiApp3;

internal static class Navegacao
{
    private static readonly HashSet<Page> PaginasEmTransicao = [];
    private static readonly SemaphoreSlim Transicao = new(1, 1);

    public static async Task IrAsync(Page origem, string rota)
    {
        if (!PaginasEmTransicao.Add(origem)) return;
        await Transicao.WaitAsync();
        try
        {
            if (Shell.Current.CurrentPage != origem) return;
            await Shell.Current.GoToAsync(rota, animate: true);
        }
        finally
        {
            Transicao.Release();
            PaginasEmTransicao.Remove(origem);
        }
    }
}
