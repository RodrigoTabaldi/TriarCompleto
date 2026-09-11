namespace MauiApp3;

public partial class AjudaPage : ContentPage
{
    public AjudaPage() => InitializeComponent();
    private async void Voltar(object? sender, EventArgs e) => await Navegacao.IrAsync(this, "..");
}
