#if WINDOWS
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;

namespace MauiApp3;

internal static class WindowsWindowChrome
{
    private static readonly ConditionalWeakTable<Microsoft.UI.Xaml.Window, object> Installed = new();

    public static void Install(Microsoft.UI.Xaml.Window nativeWindow)
    {
        if (Installed.TryGetValue(nativeWindow, out _))
            return;

        var appWindow = nativeWindow.AppWindow;
        if (appWindow.Presenter is not OverlappedPresenter presenter)
            return;

        var white = Microsoft.UI.Colors.White;
        var dark = Microsoft.UI.ColorHelper.FromArgb(255, 15, 23, 42);
        var titleBar = appWindow.TitleBar;
        titleBar.BackgroundColor = white;
        titleBar.ForegroundColor = dark;
        titleBar.ButtonBackgroundColor = white;
        titleBar.ButtonForegroundColor = dark;

        Installed.Add(nativeWindow, new object());
        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        appWindow.MoveAndResize(display.OuterBounds);
        presenter.IsAlwaysOnTop = true;
        nativeWindow.Activated += (_, args) =>
            presenter.IsAlwaysOnTop = args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated &&
                presenter.State != OverlappedPresenterState.Minimized;
    }
}
#endif
