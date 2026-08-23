namespace MauiApp3;

using MauiApp3.Models;

public partial class App : Application
{
    public static Usuario? UsuarioLogado { get; set; }

    /// <summary>
    /// True quando a sessão atual veio da Triagem Individual, sem login/cadastro.
    /// Nesse modo os dados ficam salvos apenas no aparelho.
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
