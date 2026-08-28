namespace MauiApp3;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Navigated += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentPage is not ContentPage pagina) return;

            Shell.SetNavBarIsVisible(pagina, false);
            Shell.SetTabBarIsVisible(pagina, false);
            Shell.SetBackButtonBehavior(pagina, new BackButtonBehavior { IsVisible = false });
            FullscreenButtonDecorator.Aplicar(pagina);
        });

        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
        Routing.RegisterRoute(nameof(CadastroPage), typeof(CadastroPage));
        Routing.RegisterRoute(nameof(TriagemPage), typeof(TriagemPage));
        Routing.RegisterRoute(nameof(HistoricoPage), typeof(HistoricoPage));
        Routing.RegisterRoute(nameof(ResultadoPage), typeof(ResultadoPage));
        Routing.RegisterRoute(nameof(CriarTriagemPage), typeof(CriarTriagemPage));
        Routing.RegisterRoute(nameof(SobrePage), typeof(SobrePage));
        Routing.RegisterRoute(nameof(CreditosPage), typeof(CreditosPage));
        Routing.RegisterRoute(nameof(ContatoPage), typeof(ContatoPage));
        Routing.RegisterRoute(nameof(AjudaPage), typeof(AjudaPage));

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentPage is not ContentPage pagina) return;

            Shell.SetNavBarIsVisible(pagina, false);
            Shell.SetTabBarIsVisible(pagina, false);
            Shell.SetBackButtonBehavior(pagina, new BackButtonBehavior { IsVisible = false });
            FullscreenButtonDecorator.Aplicar(pagina);
        });
    }
}
