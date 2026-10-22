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
        var shell = new AppShell();
        var window = new Window(shell);
#if WINDOWS
        window.TitleBar = new TitleBar
        {
            Title = "Triar",
            BackgroundColor = Colors.White,
            ForegroundColor = Color.FromArgb("#0F172A")
        };
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow &&
                nativeWindow.Content is Microsoft.UI.Xaml.FrameworkElement content)
            {
                content.Loaded += (_, _) => WindowsWindowChrome.Install(nativeWindow);
                if (content.IsLoaded)
                    WindowsWindowChrome.Install(nativeWindow);
            }
        };
#endif
        return window;
    }
}
