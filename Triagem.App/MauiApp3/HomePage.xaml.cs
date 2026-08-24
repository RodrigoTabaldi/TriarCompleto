using System.Collections.ObjectModel;
using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

public partial class HomePage : ContentPage
{
    private readonly ObservableCollection<TriagemResumo> _triagensDesktop = [];
    private readonly ObservableCollection<TriagemResumo> _triagensMobile = [];
    private List<TriagemResumo> _todas = [];
    private bool _modoEdicao;

    public HomePage()
    {
        InitializeComponent();

        ListaTriagensDesktop.ItemsSource = _triagensDesktop;
        ListaTriagensMobile.ItemsSource = _triagensMobile;
        SizeChanged += AjustarLayout;

        if (App.UsuarioLogado is { } u)
        {
            NomeUsuario.Text = App.ModoIndividual ? "Triagem individual" : u.Nome;
            EmailUsuario.Text = App.ModoIndividual ? "Dados salvos só neste aparelho" : u.Email;
            SaudacaoUsuarioMobile.Text = App.ModoIndividual ? "Olá" : $"Olá, {u.Nome}";
            InicialUsuarioDesktop.Text = GerarIniciais(u.Nome);
            InicialUsuarioMobile.Text = GerarIniciais(u.Nome);
        }

        BotaoSairDesktop.Text = App.ModoIndividual ? "Trocar modo de triagem" : "Sair";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarAsync();
    }

    private void AjustarLayout(object? sender, EventArgs e)
    {
        var desktop = Width >= 900;
        DesktopRoot.IsVisible = desktop;
        MobileRoot.IsVisible = !desktop;

        var span = Width >= 1100 ? 3 : Width >= 900 ? 2 : 1;
        if (LayoutGrade.Span != span)
            LayoutGrade.Span = span;
    }

    private async Task CarregarAsync()
    {
        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await Shell.Current.GoToAsync($"//{nameof(EscolhaModoPage)}");
                return;
            }

            _todas = await ApiService.ListarTriagensAsync(usuario.Id);
            foreach (var t in _todas) t.ModoEdicao = _modoEdicao;
            AplicarFiltro();
            AjustarLayout(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            if (ApiService.EhSessaoExpirada(ex))
            {
                await TratarSessaoExpiradaAsync();
                return;
            }

            await DisplayAlertAsync("Erro",
                $"Não foi possível carregar as triagens. Verifique se a API está no ar.\n\n{ex.Message}", "OK");
        }
    }

    private async Task TratarSessaoExpiradaAsync()
    {
        App.UsuarioLogado = null;
        App.ModoIndividual = false;
        ApiService.Logout();
        await DisplayAlertAsync("Sessão expirada", "Faça login novamente para continuar.", "OK");
        await Shell.Current.GoToAsync($"//{nameof(EscolhaModoPage)}");
    }

    private void AplicarFiltro()
    {
        var visiveis = _todas.Where(t => _modoEdicao || t.VisivelNaHome).ToList();

        _triagensDesktop.Clear();
        foreach (var t in visiveis)
            _triagensDesktop.Add(t);

        _triagensMobile.Clear();
        foreach (var t in (_modoEdicao ? visiveis : visiveis.Take(4)))
            _triagensMobile.Add(t);
    }

    private async void Atualizar(object? sender, EventArgs e) => await CarregarAsync();

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
        if (App.UsuarioLogado is null) return;

        var confirmar = await DisplayAlertAsync("Excluir triagem",
            $"Deseja realmente excluir \"{t.Titulo}\"? O histórico já realizado será mantido.",
            "Excluir", "Cancelar");
        if (!confirmar) return;

        var (ok, erro) = await ApiService.ExcluirTriagemAsync(t.Id);
        if (ok)
        {
            _todas.Remove(t);
            AplicarFiltro();
        }
        else
        {
            await DisplayAlertAsync("Erro", erro ?? "Não foi possível excluir.", "OK");
        }
    }

    private async void IrSobre(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SobrePage));

    private async void IrHistoricoGeral(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(HistoricoPage));

    private async void IrCreditos(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(CreditosPage));

    private async void IrContato(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(ContatoPage));

    private async void Sair(object? sender, EventArgs e)
    {
        if (App.ModoIndividual)
        {
            var trocar = await DisplayAlertAsync("Trocar modo",
                "Voltar para a tela inicial? Suas triagens continuam salvas neste aparelho.",
                "Voltar", "Cancelar");
            if (!trocar) return;

            App.UsuarioLogado = null;
            App.ModoIndividual = false;
            await Shell.Current.GoToAsync($"//{nameof(EscolhaModoPage)}");
            return;
        }

        var confirmar = await DisplayAlertAsync("Sair", "Deseja sair da sua conta?", "Sair", "Cancelar");
        if (!confirmar) return;

        App.UsuarioLogado = null;
        ApiService.Logout();
        await Shell.Current.GoToAsync($"//{nameof(EscolhaModoPage)}");
    }

    private async void AlternarEdicaoHome(object? sender, EventArgs e)
    {
        if (_modoEdicao)
        {
            await DisplayAlertAsync("Configurações",
                "Escolha as triagens que deseja exibir e use o botão Salvar configuração da home.", "OK");
            return;
        }

        _modoEdicao = true;
        BotaoSalvarHomeMobile.IsVisible = _modoEdicao;
        BotaoSalvarHomeDesktop.IsVisible = _modoEdicao;

        foreach (var t in _todas) t.ModoEdicao = _modoEdicao;
        AplicarFiltro();

        await DisplayAlertAsync("Configurações",
            "A personalização da Home foi ativada. Escolha as triagens que deseja exibir e salve ao final da lista.", "OK");
    }

    private async void SalvarConfiguracaoHome(object? sender, EventArgs e)
    {
        try
        {
            if (App.UsuarioLogado is not { } usuario) return;

            await ApiService.ConfigurarHomeAsync(usuario.Id,
                _todas.Select((t, i) => (t.Id, t.VisivelNaHome, i)));

            _modoEdicao = false;
            BotaoSalvarHomeMobile.IsVisible = false;
            BotaoSalvarHomeDesktop.IsVisible = false;
            foreach (var t in _todas) t.ModoEdicao = false;
            AplicarFiltro();

            await DisplayAlertAsync("Pronto", "Sua home foi atualizada!", "OK");
        }
        catch (Exception ex)
        {
            if (ApiService.EhSessaoExpirada(ex))
            {
                await TratarSessaoExpiradaAsync();
                return;
            }

            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }

    private static string GerarIniciais(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "U";
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0][..1].ToUpper();
        return $"{partes[0][0]}{partes[^1][0]}".ToUpper();
    }
}
