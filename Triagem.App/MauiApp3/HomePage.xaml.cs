using System.Collections.ObjectModel;
using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

public partial class HomePage : ContentPage
{
    private readonly ObservableCollection<TriagemResumo> _triagens = [];
    private List<TriagemResumo> _todas = [];
    private bool _modoEdicao;

    public HomePage()
    {
        InitializeComponent();

        ListaTriagens.ItemsSource = _triagens;
        SizeChanged += AjustarColunas;

        if (App.UsuarioLogado is { } u)
        {
            NomeUsuario.Text = u.Nome;
            EmailUsuario.Text = u.Email;
            InicialUsuario.Text = string.IsNullOrEmpty(u.Nome) ? "U" : u.Nome[..1].ToUpper();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarAsync();
    }

    /// <summary>Layout responsivo: 1 coluna no celular, 2 ou 3 no computador.</summary>
    private void AjustarColunas(object? sender, EventArgs e)
    {
        var span = Width >= 1100 ? 3 : Width >= 700 ? 2 : 1;
        if (LayoutGrade.Span != span)
            LayoutGrade.Span = span;
    }

    private async Task CarregarAsync()
    {
        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
                return;
            }

            _todas = await ApiService.ListarTriagensAsync(usuario.Id);
            foreach (var t in _todas) t.ModoEdicao = _modoEdicao;
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro",
                $"Não foi possível carregar as triagens. Verifique se a API está no ar.\n\n{ex.Message}", "OK");
        }
    }

    private void AplicarFiltro()
    {
        _triagens.Clear();
        foreach (var t in _todas.Where(t => _modoEdicao || t.VisivelNaHome))
            _triagens.Add(t);
    }

    private async void Atualizar(object? sender, EventArgs e) => await CarregarAsync();

    // ---------------- Navegação ----------------

    private async void AbrirTriagem(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is TriagemResumo t)
            await Shell.Current.GoToAsync($"{nameof(TriagemPage)}?triagemId={t.Id}");
    }

    private async void AbrirHistorico(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is TriagemResumo t)
            await Shell.Current.GoToAsync($"{nameof(HistoricoPage)}?triagemId={t.Id}&titulo={Uri.EscapeDataString(t.Titulo)}");
    }

    private async void IrCriarTriagem(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(CriarTriagemPage));

    private async void EditarTriagem(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is TriagemResumo t)
            await Shell.Current.GoToAsync($"{nameof(CriarTriagemPage)}?triagemId={t.Id}");
    }

    private async void ExcluirTriagem(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not TriagemResumo t) return;
        if (App.UsuarioLogado is not { } usuario) return;

        var confirmar = await DisplayAlertAsync("Excluir triagem",
            $"Deseja realmente excluir \"{t.Titulo}\"? O histórico já realizado será mantido.",
            "Excluir", "Cancelar");
        if (!confirmar) return;

        var (ok, erro) = await ApiService.ExcluirTriagemAsync(t.Id, usuario.Id);
        if (ok)
        {
            _todas.Remove(t);
            _triagens.Remove(t);
        }
        else
        {
            await DisplayAlertAsync("Erro", erro ?? "Não foi possível excluir.", "OK");
        }
    }

    private async void IrSobre(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SobrePage));

    private async void Sair(object? sender, EventArgs e)
    {
        var confirmar = await DisplayAlertAsync("Sair", "Deseja sair da sua conta?", "Sair", "Cancelar");
        if (!confirmar) return;

        App.UsuarioLogado = null;
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }

    // ---------------- Edição da home ----------------

    private void AlternarEdicaoHome(object? sender, EventArgs e)
    {
        _modoEdicao = !_modoEdicao;
        BotaoEditarHome.Text = _modoEdicao ? "✕ Cancelar edição" : "⚙ Editar home";
        BotaoSalvarHome.IsVisible = _modoEdicao;

        foreach (var t in _todas) t.ModoEdicao = _modoEdicao;
        AplicarFiltro();
    }

    private async void SalvarConfiguracaoHome(object? sender, EventArgs e)
    {
        try
        {
            if (App.UsuarioLogado is not { } usuario) return;

            await ApiService.ConfigurarHomeAsync(usuario.Id,
                _todas.Select((t, i) => (t.Id, t.VisivelNaHome, i)));

            _modoEdicao = false;
            BotaoEditarHome.Text = "⚙ Editar home";
            BotaoSalvarHome.IsVisible = false;
            foreach (var t in _todas) t.ModoEdicao = false;
            AplicarFiltro();

            await DisplayAlertAsync("Pronto", "Sua home foi atualizada!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }
}
