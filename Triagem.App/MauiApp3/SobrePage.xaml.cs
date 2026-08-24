namespace MauiApp3;

public partial class SobrePage : ContentPage
{
    public SobrePage()
    {
        InitializeComponent();
    }

    private async void Voltar(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");

    private async void AbrirNormas(object? sender, EventArgs e) =>
        await Browser.Default.OpenAsync(
            "https://ufr.edu.br/propgp/docs-legis-pesq/",
            BrowserLaunchMode.SystemPreferred);
}
