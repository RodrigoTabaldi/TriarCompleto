namespace MauiApp3;

using MauiApp3.Models;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using WinRT.Interop;
#elif ANDROID
using Microsoft.Maui.ApplicationModel;
#endif

public partial class App : Application
{
    private static bool _telaCheia = true;

    public static bool TelaCheia => _telaCheia;

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
                nativeWindow.ExtendsContentIntoTitleBar = true;
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

    public static void MinimizarJanela()
    {
#if WINDOWS
        if (!TentarObterAppWindow(out var appWindow)) return;

        _telaCheia = false;
        AplicarTelaCheia(appWindow, false);
        if (appWindow.Presenter is OverlappedPresenter presenter)
            presenter.Minimize();
#endif
    }

    public static void FecharJanela()
    {
        if (Current is { Windows.Count: > 0 })
            Current.CloseWindow(Current.Windows[0]);
    }

    public static bool AlternarMaximizada()
    {
#if WINDOWS
        if (TentarObterAppWindow(out var appWindow) &&
            appWindow.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                presenter.Restore();
                return false;
            }

            presenter.Maximize();
            return true;
        }
#endif
        return false;
    }

    public static void IniciarArrasteJanela()
    {
#if WINDOWS
        if (Current is not { Windows.Count: > 0 } ||
            Current.Windows[0].Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
            return;

        var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
        ReleaseCapture();
        SendMessage(windowHandle, 0x00A1, (IntPtr)2, IntPtr.Zero);
#endif
    }

#if WINDOWS
    private static void AplicarTelaCheia(AppWindow appWindow, bool telaCheia)
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        appWindow.SetPresenter(telaCheia
            ? AppWindowPresenterKind.FullScreen
            : AppWindowPresenterKind.Default);

        if (!telaCheia && appWindow.Presenter is OverlappedPresenter presenter)
            presenter.SetBorderAndTitleBar(true, false);
    }

    private static bool TentarObterAppWindow(out AppWindow appWindow)
    {
        appWindow = null!;
        if (Current is not { Windows.Count: > 0 } ||
            Current.Windows[0].Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
            return false;

        var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
#endif
}
