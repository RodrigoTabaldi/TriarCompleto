namespace MauiApp3;

public partial class ContatoPage : ContentPage
{
    public ContatoPage() => InitializeComponent();
    private async void Voltar(object? sender, EventArgs e) => await Navegacao.IrAsync(this, "..");
    private async void EnviarEmail(object? sender, EventArgs e) => await Launcher.Default.OpenAsync("mailto:triarcontato@gmail.com");
}
