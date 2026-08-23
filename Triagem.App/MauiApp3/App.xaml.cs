namespace MauiApp3;

using MauiApp3.Models;

public partial class App : Application
{
    public static Usuario? UsuarioLogado { get; set; }

    /// <summary>
    /// True quando a sessão atual veio da Triagem Individual (sem login/cadastro,
    /// dados só no aparelho) em vez do fluxo normal de conta.
    /// </summary>
    public static bool ModoIndividual { get; set; }

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
