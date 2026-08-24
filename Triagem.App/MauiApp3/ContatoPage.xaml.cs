namespace MauiApp3;

public partial class ContatoPage : ContentPage
{
    public ContatoPage() => InitializeComponent();
    private async void Voltar(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private async void EnviarEmail(object? sender, EventArgs e) => await Launcher.Default.OpenAsync("mailto:pesquisa.propgp@ufr.edu.br");
    private async void AbrirContatos(object? sender, EventArgs e) => await Browser.Default.OpenAsync("https://ufr.edu.br/propgp/contato/", BrowserLaunchMode.SystemPreferred);
}
