namespace MauiApp3;

public partial class SplashPage : ContentPage
{
    private bool _animada;

    public SplashPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_animada) return;
        _animada = true;

        await Task.WhenAll(
            Logo.FadeToAsync(1, 420, Easing.CubicOut),
            Logo.ScaleToAsync(1, 520, Easing.CubicOut));

        await Task.WhenAll(
            Indicador.FadeToAsync(1, 220, Easing.CubicOut),
            Indicador.ScaleXToAsync(1, 360, Easing.CubicOut));

        await Task.Delay(280);
        await Shell.Current.GoToAsync($"//{nameof(EscolhaModoPage)}", animate: true);
    }
}
