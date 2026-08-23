using MauiApp3.Services;

namespace MauiApp3;

public partial class LoginPage : ContentPage
{
    private bool _entrando;
    private bool _tentouRestaurarSessao;

    public LoginPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Só tenta uma vez por instância da página: depois de um logout explícito o
        // usuário volta para cá com a sessão já limpa do SecureStorage, então a
        // segunda tentativa (ex. ao navegar de volta) não deve reentrar em loop.
        if (_tentouRestaurarSessao) return;
        _tentouRestaurarSessao = true;

        var usuario = await ApiService.RestaurarSessaoAsync();
        if (usuario is null) return;

        App.UsuarioLogado = usuario;

        // Rota relativa, e não "//HomePage": a HomePage é registrada por
        // Routing.RegisterRoute (rota global), e o Shell não aceita rota global como
        // única página da pilha — a forma absoluta lançava exceção e derrubava o app
        // sempre que havia uma sessão salva para restaurar.
        await Shell.Current.GoToAsync(nameof(HomePage));
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
