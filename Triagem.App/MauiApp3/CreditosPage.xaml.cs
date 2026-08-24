namespace MauiApp3;

public partial class CreditosPage : ContentPage
{
    public CreditosPage() => InitializeComponent();
    private async void Voltar(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
