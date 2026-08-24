using MauiApp3.Services;

namespace MauiApp3;

/// <summary>
/// Primeira tela do app: decide se a pessoa vai direto para uma Triagem Individual
/// sem login ou para o fluxo com conta da Triagem em Grupo.
/// </summary>
public partial class EscolhaModoPage : ContentPage
{
    private enum Modo { Nenhum, Individual, Grupo }

    private Modo _modoEscolhido = Modo.Nenhum;
    private bool _entrando;

    private static readonly Color CorBordaPadrao = Color.FromArgb("#E5E7EB");
    private static readonly Color CorBordaSelecionada = Color.FromArgb("#10B981");
    private static readonly Color CorFundoSelecionado = Color.FromArgb("#F0FDF9");

    public EscolhaModoPage()
    {
        InitializeComponent();
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
                App.UsuarioLogado = await ApiService.IniciarModoIndividualAsync();
                App.ModoIndividual = true;
                await Shell.Current.GoToAsync(nameof(HomePage));
            }
            else
            {
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
