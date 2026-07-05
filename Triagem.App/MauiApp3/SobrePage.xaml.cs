namespace MauiApp3;

public partial class SobrePage : ContentPage
{
    public SobrePage()
    {
        InitializeComponent();
    }

    private async void Voltar(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
