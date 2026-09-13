namespace MauiApp3;

using MauiApp3.Services;

public partial class SobrePage : ContentPage
{
    public SobrePage()
    {
        InitializeComponent();
    }

    private async void Voltar(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, "..");

    private async void AbrirNormas(object? sender, EventArgs e) =>
        await Browser.Default.OpenAsync(
            "https://ufr.edu.br/propgp/docs-legis-pesq/",
            BrowserLaunchMode.SystemPreferred);

    private async void ExportarDados(object? sender, EventArgs e)
    {
        string? arquivo = null;
        try
        {
            var json = await ApiService.ExportarDadosJsonAsync();
            arquivo = Path.Combine(FileSystem.Current.CacheDirectory, $"triar-dados-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(arquivo, json);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Exportar meus dados do Triar",
                File = new ShareFile(arquivo)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", $"Não foi possível exportar os dados.\n\n{ex.Message}", "OK");
        }
        finally
        {
            if (arquivo is not null && File.Exists(arquivo))
                try { File.Delete(arquivo); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private async void ExcluirConta(object? sender, EventArgs e)
    {
        var senha = await DisplayPromptAsync("Excluir conta", "Confirme sua senha para continuar.",
            "Continuar", "Cancelar", keyboard: Keyboard.Default);
        if (senha is null) return;
        var confirmar = await DisplayAlertAsync("Exclusão permanente",
            "A conta, o histórico e as triagens personalizadas serão apagados definitivamente. Deseja continuar?",
            "Excluir definitivamente", "Cancelar");
        if (!confirmar) return;

        var (ok, erro) = await ApiService.ExcluirContaAsync(senha);
        if (!ok)
        {
            await DisplayAlertAsync("Não foi possível excluir", erro ?? "Tente novamente.", "OK");
            return;
        }

        App.UsuarioLogado = null;
        App.ModoIndividual = false;
        ApiService.Logout();
        await Navegacao.IrAsync(this, $"//{nameof(EscolhaModoPage)}");
    }
}
