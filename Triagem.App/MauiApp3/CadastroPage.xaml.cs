using MauiApp3.Services;

namespace MauiApp3;

public partial class CadastroPage : ContentPage
{
    private bool _enviando;

    public CadastroPage()
    {
        InitializeComponent();
    }

    private async void Cadastrar(object? sender, EventArgs e)
    {
        if (_enviando) return;

        try
        {
            if (string.IsNullOrWhiteSpace(Nome.Text) ||
                string.IsNullOrWhiteSpace(Email.Text) ||
                string.IsNullOrWhiteSpace(Senha.Text))
            {
                await DisplayAlertAsync("Atenção", "Preencha todos os campos.", "OK");
                return;
            }

            _enviando = true;

            var (usuario, erro) = await ApiService.RegistrarAsync(
                Nome.Text.Trim(), Email.Text.Trim(), Senha.Text);

            if (usuario is not null)
            {
                // Cadastro já autentica (a API devolve o token). Entra direto na home.
                App.UsuarioLogado = usuario;
                App.ModoIndividual = false;
                await Shell.Current.GoToAsync(nameof(HomePage));
            }
            else
            {
                await DisplayAlertAsync("Erro", erro ?? "Não foi possível cadastrar.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro",
                $"Não foi possível conectar à API. Verifique se ela está no ar.\n\n{ex.Message}", "OK");
        }
        finally
        {
            _enviando = false;
        }
    }

    private async void IrLogin(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
