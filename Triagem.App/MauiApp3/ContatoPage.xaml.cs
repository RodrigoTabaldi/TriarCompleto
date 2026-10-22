namespace MauiApp3;

public partial class ContatoPage : ContentPage
{
    public ContatoPage() => InitializeComponent();
    private async void Voltar(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private async void EnviarEmailTriar(object? sender, EventArgs e) => await Launcher.Default.OpenAsync("mailto:triarcontato@gmail.com");
    private async void EnviarEmailDesenvolvedor(object? sender, EventArgs e) => await Launcher.Default.OpenAsync("mailto:rodrigotabaldi01@gmail.com");
}
