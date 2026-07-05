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

            var (ok, erro) = await ApiService.RegistrarAsync(
                Nome.Text.Trim(), Email.Text.Trim(), Senha.Text);

            if (ok)
            {
                await DisplayAlertAsync("Sucesso", "Cadastro realizado! Faça login para continuar.", "OK");
                await Shell.Current.GoToAsync("..");
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
