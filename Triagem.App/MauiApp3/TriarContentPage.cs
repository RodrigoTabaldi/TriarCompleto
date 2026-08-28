namespace MauiApp3;

internal static class TriarDialogs
{
    public static Task<bool> ExibirAsync(Page origem, string title, string message, string accept) =>
        TriarDialogPage.ExibirAsync(origem, title, message, accept, null);

    public static Task<bool> ExibirAsync(
        Page origem, string title, string message, string accept, string cancel) =>
        TriarDialogPage.ExibirAsync(origem, title, message, accept, cancel);
}

internal sealed class TriarDialogPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _fechando;

    private TriarDialogPage(string titulo, string mensagem, string aceitar, string? cancelar)
    {
        BackgroundColor = Color.FromArgb("#7307142E");
        Shell.SetNavBarIsVisible(this, false);

        var tituloNormalizado = titulo.ToLowerInvariant();
        var ehErro = tituloNormalizado.Contains("erro") || tituloNormalizado.Contains("expirada");
        var ehDestrutivo = tituloNormalizado.Contains("sair") || tituloNormalizado.Contains("excluir");
        var ehAtencao = tituloNormalizado.Contains("atenção") || tituloNormalizado.Contains("formato") ||
                        tituloNormalizado.Contains("grande");
        var ehSucesso = tituloNormalizado.Contains("sucesso") || tituloNormalizado.Contains("pronto");
        var ehConfirmacao = cancelar is not null;

        var corDestaque = ehErro || ehDestrutivo
            ? Color.FromArgb("#DC2626")
            : ehAtencao
                ? Color.FromArgb("#D97706")
                : Color.FromArgb("#009B72");
        var simbolo = ehErro || ehAtencao ? "!" : ehSucesso ? "✓" : ehConfirmacao ? "?" : "i";

        var icone = new Border
        {
            WidthRequest = 46,
            HeightRequest = 46,
            BackgroundColor = corDestaque.WithAlpha(0.12f),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 23 },
            Content = new Label
            {
                Text = simbolo,
                FontSize = 23,
                FontAttributes = FontAttributes.Bold,
                TextColor = corDestaque,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var cabecalho = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 14
        };
        cabecalho.Children.Add(icone);
        var tituloLabel = new Label
        {
            Text = titulo,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#07142E"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(tituloLabel, 1);
        cabecalho.Children.Add(tituloLabel);

        var mensagemLabel = new Label
        {
            Text = mensagem,
            FontSize = 15,
            LineHeight = 1.25,
            TextColor = Color.FromArgb("#40506A")
        };

        var marca = new Image
        {
            Source = "logo.png",
            HeightRequest = 38,
            HorizontalOptions = LayoutOptions.Start,
            Aspect = Aspect.AspectFit
        };

        var botaoAceitar = CriarBotao(aceitar, corDestaque, preenchido: true);
        botaoAceitar.Clicked += async (_, _) => await FecharAsync(true);

        var botoes = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 8, 0, 0) };
        if (cancelar is null)
        {
            botoes.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            botoes.Children.Add(botaoAceitar);
        }
        else
        {
            botoes.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            botoes.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var botaoCancelar = CriarBotao(cancelar, Color.FromArgb("#40506A"), preenchido: false);
            botaoCancelar.Clicked += async (_, _) => await FecharAsync(false);
            botoes.Children.Add(botaoCancelar);
            Grid.SetColumn(botaoAceitar, 1);
            botoes.Children.Add(botaoAceitar);
        }

        var conteudo = new VerticalStackLayout
        {
            Spacing = 18,
            Children = { marca, cabecalho, mensagemLabel, botoes }
        };

        var cartao = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E5EAF0"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(26, 24),
            MaximumWidthRequest = 440,
            MinimumWidthRequest = 320,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#3307142E")),
                Offset = new Point(0, 8),
                Radius = 22,
                Opacity = 0.35f
            },
            Content = conteudo
        };

        Content = new Grid
        {
            Padding = new Thickness(24),
            Children = { cartao }
        };
    }

    public static async Task<bool> ExibirAsync(
        Page origem, string titulo, string mensagem, string aceitar, string? cancelar)
    {
        var dialogo = new TriarDialogPage(titulo, mensagem, aceitar, cancelar);
        await origem.Navigation.PushModalAsync(dialogo, false);
        return await dialogo._resultado.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = FecharAsync(false);
        return true;
    }

    private static Button CriarBotao(string texto, Color cor, bool preenchido) => new()
    {
        Text = texto,
        HeightRequest = 46,
        CornerRadius = 9,
        FontAttributes = FontAttributes.Bold,
        FontSize = 15,
        BackgroundColor = preenchido ? cor : Colors.White,
        TextColor = preenchido ? Colors.White : cor,
        BorderColor = cor,
        BorderWidth = 1,
        Padding = new Thickness(14, 0)
    };

    private async Task FecharAsync(bool resultado)
    {
        if (_fechando) return;
        _fechando = true;
        _resultado.TrySetResult(resultado);
        await Navigation.PopModalAsync(false);
    }
}
