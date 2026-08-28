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
                OcultarBarraNativa(nativeWindow);
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
            OcultarBarraNativa(nativeWindow);
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
        if (Current?.Windows[0].Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            OcultarBarraNativa(nativeWindow);
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
                if (Current?.Windows[0].Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                    OcultarBarraNativa(nativeWindow);
                return false;
            }

            presenter.Maximize();
            if (Current?.Windows[0].Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindowMaximizada)
                OcultarBarraNativa(nativeWindowMaximizada);
            return true;
        }
#endif
        return false;
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
            presenter.SetBorderAndTitleBar(false, false);
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

    private static void OcultarBarraNativa(Microsoft.UI.Xaml.Window nativeWindow)
    {
        const int gwlStyle = -16;
        const long wsCaption = 0x00C00000L;
        const long wsThickFrame = 0x00040000L;
        const uint frameChanged = 0x0020;
        const uint noMove = 0x0002;
        const uint noSize = 0x0001;
        const uint noZOrder = 0x0004;
        const uint noActivate = 0x0010;

        var handle = WindowNative.GetWindowHandle(nativeWindow);
        var estilo = GetWindowLongPtr(handle, gwlStyle).ToInt64();
        SetWindowLongPtr(handle, gwlStyle, new IntPtr(estilo & ~wsCaption & ~wsThickFrame));
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
            frameChanged | noMove | noSize | noZOrder | noActivate);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

#endif
}
