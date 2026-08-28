using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace MauiApp3
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private bool _telaCheia = true;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AplicarTelaCheia();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);

            if (hasFocus)
            {
                AplicarTelaCheia();
            }
        }

        public void SetTelaCheia(bool telaCheia)
        {
            _telaCheia = telaCheia;
            AplicarTelaCheia();
        }

        private void AplicarTelaCheia()
        {
            if (Window is null) return;

            if (_telaCheia)
            {
                Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
            }
            else
            {
                Window.ClearFlags(WindowManagerFlags.Fullscreen);
            }

            if (Window.DecorView is not null)
            {
#pragma warning disable CA1422
                Window.DecorView.SystemUiFlags = _telaCheia
                    ? SystemUiFlags.Fullscreen
                        | SystemUiFlags.HideNavigation
                        | SystemUiFlags.ImmersiveSticky
                        | SystemUiFlags.LayoutFullscreen
                        | SystemUiFlags.LayoutHideNavigation
                        | SystemUiFlags.LayoutStable
                    : SystemUiFlags.Visible;
#pragma warning restore CA1422
            }
        }
    }
}
