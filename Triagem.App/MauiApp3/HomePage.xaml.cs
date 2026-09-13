using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

public partial class HomePage : ContentPage
{
    private List<TriagemResumo> _todas = [];
    private bool _modoEdicao;
    private bool _carregando;
    private bool _carregada;
    private int _versaoCarregada = -1;
    private bool? _layoutDesktop;

    public HomePage()
    {
        InitializeComponent();

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
        await CarregarAsync(forceRefresh: false);
    }

    private void AjustarLayout(object? sender, EventArgs e)
    {
        // PC: barra lateral e exatamente três triagens por linha.
        // Abaixo do breakpoint, troca por uma coluna e navegação inferior.
        // Em um desktop em tela cheia, a grade deve permanecer 3x2 independentemente
        // da escala de exibição configurada no Windows. Fora da tela cheia, usa a largura.
        var desktopEmTelaCheia = DeviceInfo.Current.Idiom == DeviceIdiom.Desktop && App.TelaCheia;
        var desktop = desktopEmTelaCheia || Width >= 1000;
        if (_layoutDesktop == desktop) return;

        _layoutDesktop = desktop;
        DesktopRoot.IsVisible = desktop;
        MobileRoot.IsVisible = !desktop;
        AplicarFiltro();
    }

    internal void AtualizarLayoutResponsivo() => AjustarLayout(this, EventArgs.Empty);

    private async Task CarregarAsync(bool forceRefresh)
    {
        if (_carregando) return;
        if (!forceRefresh && _carregada && _versaoCarregada == ApiService.VersaoTriagens)
            return;

        _carregando = true;
        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await Navegacao.IrAsync(this, $"//{nameof(EscolhaModoPage)}");
                return;
            }

            // Modais e retornos não devem apagar alterações ainda não salvas.
            if (_modoEdicao) return;
            var carregadas = await ApiService.ListarTriagensAsync(usuario.Id, forceRefresh);
            if (_todas.Count > 0 && _todas.Count == carregadas.Count &&
                _todas.Zip(carregadas).All(par =>
                    par.First.Id == par.Second.Id &&
                    par.First.Titulo == par.Second.Titulo &&
                    par.First.PublicoAlvo == par.Second.PublicoAlvo &&
                    par.First.Imagem == par.Second.Imagem &&
                    par.First.MinhaAutoria == par.Second.MinhaAutoria &&
                    par.First.VisivelNaHome == par.Second.VisivelNaHome))
            {
                _carregada = true;
                _versaoCarregada = ApiService.VersaoTriagens;
                return;
            }
            _todas = carregadas;
            await Task.Run(() =>
            {
                // Imagens personalizadas chegam em Base64. Prepará-las fora da thread
                // visual evita decodificações pesadas enquanto o usuário rola a lista.
                foreach (var triagem in _todas)
                    _ = triagem.ImagemHome;
            });
            foreach (var t in _todas) t.ModoEdicao = _modoEdicao;
            AplicarFiltro();
            AjustarLayout(this, EventArgs.Empty);
            _carregada = true;
            _versaoCarregada = ApiService.VersaoTriagens;
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
        finally
        {
            _carregando = false;
        }
    }

    private async Task TratarSessaoExpiradaAsync()
    {
        App.UsuarioLogado = null;
        App.ModoIndividual = false;
        ApiService.Logout();
        await DisplayAlertAsync("Sessão expirada", "Faça login novamente para continuar.", "OK");
        await Navegacao.IrAsync(this, $"//{nameof(EscolhaModoPage)}");
    }

    private void AplicarFiltro()
    {
        var visiveis = _todas.Where(t => _modoEdicao || t.VisivelNaHome).ToList();

        var linhasDesktop = new List<LinhaTriagens>((visiveis.Count + 2) / 3);
        for (var i = 0; i < visiveis.Count; i += 3)
        {
            linhasDesktop.Add(new LinhaTriagens
            {
                Primeira = visiveis[i],
                Segunda = i + 1 < visiveis.Count ? visiveis[i + 1] : null,
                Terceira = i + 2 < visiveis.Count ? visiveis[i + 2] : null
            });
        }

        // Uma troca de fonte gera apenas uma atualização visual. Limpar e adicionar
        // item a item fazia a CollectionView recalcular o layout repetidas vezes.
        // A lista invisível não precisa criar cartões, bindings e imagens.
        ListaTriagensDesktop.ItemsSource = _layoutDesktop == true ? linhasDesktop : null;
        ListaTriagensMobile.ItemsSource = _layoutDesktop == true ? null : visiveis;
    }

    private async void Atualizar(object? sender, EventArgs e) => await CarregarAsync(forceRefresh: true);

    private void TocarInicio(object? sender, EventArgs e)
    {
        // A aba ativa não deve repetir a consulta nem reconstruir todos os cartões.
        // A página e sua posição permanecem intactas.
    }

    private async void AbrirTriagem(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is TriagemResumo t)
            await Navegacao.IrAsync(this, $"{nameof(TriagemPage)}?triagemId={t.Id}");
    }

    private async void AbrirHistorico(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is TriagemResumo t)
            await Navegacao.IrAsync(this, $"{nameof(HistoricoPage)}?triagemId={t.Id}&titulo={Uri.EscapeDataString(t.Titulo)}");
    }

    private async void IrCriarTriagem(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, nameof(CriarTriagemPage));

    private async void EditarTriagem(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is TriagemResumo t)
            await Navegacao.IrAsync(this, $"{nameof(CriarTriagemPage)}?triagemId={t.Id}");
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
            _versaoCarregada = ApiService.VersaoTriagens;
            AplicarFiltro();
        }
        else
        {
            await DisplayAlertAsync("Erro", erro ?? "Não foi possível excluir.", "OK");
        }
    }

    private async void IrSobre(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, nameof(SobrePage));

    private async void IrHistoricoGeral(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, nameof(HistoricoPage));

    private async void IrCreditos(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, nameof(CreditosPage));

    private async void IrContato(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, nameof(ContatoPage));

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
            await Navegacao.IrAsync(this, $"//{nameof(EscolhaModoPage)}");
            return;
        }

        var confirmar = await DisplayAlertAsync("Sair", "Deseja sair da sua conta?", "Sair", "Cancelar");
        if (!confirmar) return;

        App.UsuarioLogado = null;
        ApiService.Logout();
        await Navegacao.IrAsync(this, $"//{nameof(EscolhaModoPage)}");
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
            _versaoCarregada = ApiService.VersaoTriagens;
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
        if (partes.Length == 1) return partes[0][..1].ToUpperInvariant();
        return $"{partes[0][0]}{partes[^1][0]}".ToUpperInvariant();
    }
}
