#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Button = Microsoft.UI.Xaml.Controls.Button;
using ColumnDefinition = Microsoft.UI.Xaml.Controls.ColumnDefinition;
using FrameworkElement = Microsoft.UI.Xaml.FrameworkElement;
using Grid = Microsoft.UI.Xaml.Controls.Grid;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;
using RowDefinition = Microsoft.UI.Xaml.Controls.RowDefinition;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using Thickness = Microsoft.UI.Xaml.Thickness;
using AutomationProperties = Microsoft.UI.Xaml.Automation.AutomationProperties;

namespace MauiApp3;

internal static class WindowsWindowChrome
{
    private const int SwMinimize = 6;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    public static void Install(Microsoft.UI.Xaml.Window nativeWindow)
    {
        if (nativeWindow.Content is not FrameworkElement content || content.Tag is "TriarChrome")
            return;

        var appWindow = nativeWindow.AppWindow;
        var white = new SolidColorBrush(Microsoft.UI.Colors.White);
        var dark = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 15, 23, 42));
        var red = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68));

        Button MakeButton(string symbol, string name, SolidColorBrush color)
        {
            var button = new Button
            {
                Content = symbol,
                Width = 46,
                Height = 32,
                Padding = new Thickness(0),
                Background = white,
                Foreground = color,
                BorderThickness = new Thickness(0),
                FontSize = 13
            };
            AutomationProperties.SetName(button, name);
            return button;
        }

        var minimize = MakeButton("─", "Minimizar", dark);
        var switchMode = MakeButton("□", "Alternar tela cheia", dark);
        var close = MakeButton("×", "Fechar", red);

        var header = new Grid { Background = white, Height = 32 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 3; i++)
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        Grid.SetColumn(minimize, 1);
        Grid.SetColumn(switchMode, 2);
        Grid.SetColumn(close, 3);
        header.Children.Add(minimize);
        header.Children.Add(switchMode);
        header.Children.Add(close);

        var root = new Grid { Tag = "TriarChrome", Background = white };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        nativeWindow.Content = null;
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        root.Children.Add(header);
        nativeWindow.Content = root;

        void SetWindowed()
        {
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
                presenter.SetBorderAndTitleBar(true, false);
            var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var width = Math.Min(1200, workArea.Width);
            var height = Math.Min(800, workArea.Height);
            appWindow.MoveAndResize(new RectInt32(
                workArea.X + (workArea.Width - width) / 2,
                workArea.Y + (workArea.Height - height) / 2,
                width, height));
            switchMode.Content = "□";
        }

        void ToggleFullScreen()
        {
            if (appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
                SetWindowed();
            else
            {
                appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                switchMode.Content = "▢";
            }
        }

        minimize.Click += (_, _) => ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow), SwMinimize);
        switchMode.Click += (_, _) => ToggleFullScreen();
        close.Click += (_, _) => nativeWindow.Close();
        root.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.F11 ||
                (e.Key == Windows.System.VirtualKey.Escape &&
                 appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen))
            {
                ToggleFullScreen();
                e.Handled = true;
            }
        };

        root.DispatcherQueue.TryEnqueue(() =>
        {
            if (appWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
                ToggleFullScreen();
        });
    }
}
#endif
