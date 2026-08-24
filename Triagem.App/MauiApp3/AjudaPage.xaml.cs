namespace MauiApp3;

public partial class AjudaPage : ContentPage
{
    public AjudaPage() => InitializeComponent();
    private async void Voltar(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
