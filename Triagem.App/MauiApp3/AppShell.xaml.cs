namespace MauiApp3;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
        Routing.RegisterRoute(nameof(CadastroPage), typeof(CadastroPage));
        Routing.RegisterRoute(nameof(TriagemPage), typeof(TriagemPage));
        Routing.RegisterRoute(nameof(HistoricoPage), typeof(HistoricoPage));
        Routing.RegisterRoute(nameof(ResultadoPage), typeof(ResultadoPage));
        Routing.RegisterRoute(nameof(CriarTriagemPage), typeof(CriarTriagemPage));
        Routing.RegisterRoute(nameof(SobrePage), typeof(SobrePage));
    }
}
