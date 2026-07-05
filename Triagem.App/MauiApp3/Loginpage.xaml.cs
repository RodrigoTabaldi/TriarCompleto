using MauiApp3.Services;

namespace MauiApp3;

public partial class LoginPage : ContentPage
{
    private bool _entrando;

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void Entrar(object? sender, EventArgs e)
    {
        if (_entrando) return;

        try
        {
            if (string.IsNullOrWhiteSpace(Email.Text) || string.IsNullOrWhiteSpace(Senha.Text))
            {
                await DisplayAlertAsync("Atenção", "Preencha email e senha.", "OK");
                return;
            }

            _entrando = true;

            var usuario = await ApiService.LoginAsync(Email.Text.Trim(), Senha.Text);

            if (usuario is null)
            {
                await DisplayAlertAsync("Erro", "Email ou senha inválidos.", "OK");
                return;
            }

            App.UsuarioLogado = usuario;
            Senha.Text = "";

            await Shell.Current.GoToAsync(nameof(HomePage));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro",
                $"Não foi possível conectar à API. Verifique se ela está no ar.\n\n{ex.Message}", "OK");
        }
        finally
        {
            _entrando = false;
        }
    }

    private async void IrCadastro(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(CadastroPage));
}
