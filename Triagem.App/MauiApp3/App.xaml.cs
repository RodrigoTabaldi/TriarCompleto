namespace MauiApp3;

using MauiApp3.Models;

public partial class App : Application
{
    public static Usuario? UsuarioLogado { get; set; }

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
