namespace MauiApp3;

using MauiApp3.Models;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#elif ANDROID
using Microsoft.Maui.ApplicationModel;
#endif

public partial class App : Application
{
    private static bool _telaCheia = true;

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
        var window = new Window(new AppShell());

#if WINDOWS
        window.Created += (_, _) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                AplicarTelaCheia(appWindow, _telaCheia);
            }
        };
#endif

        return window;
    }

    public static bool AlternarTelaCheia()
    {
        _telaCheia = !_telaCheia;

#if WINDOWS
        if (Current is { Windows.Count: > 0 }
            && Current.Windows[0].Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            AplicarTelaCheia(appWindow, _telaCheia);
        }
#elif ANDROID
        if (Platform.CurrentActivity is MainActivity activity)
        {
            activity.SetTelaCheia(_telaCheia);
        }
#endif

        return _telaCheia;
    }

#if WINDOWS
    private static void AplicarTelaCheia(AppWindow appWindow, bool telaCheia)
    {
        appWindow.SetPresenter(telaCheia
            ? AppWindowPresenterKind.FullScreen
            : AppWindowPresenterKind.Default);
    }
#endif
}
