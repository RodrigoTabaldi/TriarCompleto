using MauiApp3.Services;

namespace MauiApp3;

/// <summary>
/// Primeira tela do app: decide se a pessoa vai direto para uma Triagem Individual
/// (sem login, dados só no aparelho) ou para o fluxo normal com conta (Triagem em
/// Grupo). Fica antes do login na navegação do Shell.
/// </summary>
public partial class EscolhaModoPage : ContentPage
{
    private enum Modo { Nenhum, Individual, Grupo }

    private Modo _modoEscolhido = Modo.Nenhum;
    private bool _entrando;
    private bool _tentouRestaurarSessao;

    private static readonly Color CorBordaPadrao = Color.FromArgb("#E5E7EB");
    private static readonly Color CorBordaSelecionada = Color.FromArgb("#10B981");
    private static readonly Color CorFundoSelecionado = Color.FromArgb("#F0FDF9");

    public EscolhaModoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Só tenta uma vez por instância da página, mesma lógica que a LoginPage usava
        // antes de esta tela existir: evita reentrar em loop ao voltar para cá depois
        // de um logout explícito.
        if (_tentouRestaurarSessao) return;
        _tentouRestaurarSessao = true;

        // Se já existe uma conta logada (fluxo em grupo) com sessão válida, pula direto
        // para a home em vez de pedir para escolher de novo. A Triagem Individual não
        // usa essa sessão (não guarda token), então quem escolheu Individual sempre
        // volta a ver esta tela na próxima abertura do app.
        var usuario = await ApiService.RestaurarSessaoAsync();
        if (usuario is null) return;

        App.UsuarioLogado = usuario;
        App.ModoIndividual = false;

        // Rota relativa, e não "//HomePage": a HomePage é registrada por
        // Routing.RegisterRoute (rota global), e o Shell não aceita rota global como
        // única página da pilha — a forma absoluta lançava exceção e derrubava o app.
        await Shell.Current.GoToAsync(nameof(HomePage));
    }

    private void SelecionarIndividual(object? sender, EventArgs e) => Selecionar(Modo.Individual);

    private void SelecionarGrupo(object? sender, EventArgs e) => Selecionar(Modo.Grupo);

    private void Selecionar(Modo modo)
    {
        _modoEscolhido = modo;
        BotaoContinuar.IsEnabled = true;

        CardIndividual.Stroke = modo == Modo.Individual ? CorBordaSelecionada : CorBordaPadrao;
        CardIndividual.StrokeThickness = modo == Modo.Individual ? 2.5 : 1.5;
        CardIndividual.BackgroundColor = modo == Modo.Individual ? CorFundoSelecionado : Colors.White;

        CardGrupo.Stroke = modo == Modo.Grupo ? CorBordaSelecionada : CorBordaPadrao;
        CardGrupo.StrokeThickness = modo == Modo.Grupo ? 2.5 : 1.5;
        CardGrupo.BackgroundColor = modo == Modo.Grupo ? CorFundoSelecionado : Colors.White;
    }

    private async void Continuar(object? sender, EventArgs e)
    {
        if (_entrando || _modoEscolhido == Modo.Nenhum) return;

        try
        {
            _entrando = true;
            BotaoContinuar.IsEnabled = false;

            if (_modoEscolhido == Modo.Individual)
            {
                // Sem login, sem cadastro: entra direto na home de triagens. Os dados
                // ficam só no aparelho (ver ApiService.IniciarModoIndividualAsync).
                App.UsuarioLogado = await ApiService.IniciarModoIndividualAsync();
                App.ModoIndividual = true;

                // Rota relativa: ver comentário em OnAppearing sobre por que "//HomePage"
                // (rota absoluta) derruba o app.
                await Shell.Current.GoToAsync(nameof(HomePage));
            }
            else
            {
                // Rota relativa: LoginPage não é ShellContent (ver comentário no
                // AppShell.xaml sobre por que dois ShellContent derrubavam o app).
                await Shell.Current.GoToAsync(nameof(LoginPage));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", $"Não foi possível continuar.\n\n{ex.Message}", "OK");
            BotaoContinuar.IsEnabled = true;
        }
        finally
        {
            _entrando = false;
        }
    }
}
