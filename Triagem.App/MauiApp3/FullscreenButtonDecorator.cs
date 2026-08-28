namespace MauiApp3;

/// <summary>
/// Mantém os controles de janela flutuando sobre o conteúdo, sem criar uma barra própria.
/// </summary>
internal static class FullscreenButtonDecorator
{
    private static readonly BindableProperty DecoradaProperty = BindableProperty.CreateAttached(
        "Decorada", typeof(bool), typeof(FullscreenButtonDecorator), false);

    public static void Aplicar(ContentPage? pagina)
    {
        if (pagina?.Content is not View conteudo || (bool)pagina.GetValue(DecoradaProperty))
            return;

        pagina.Content = null;
#if WINDOWS
        AplicarBarraWindows(pagina, conteudo);
#else
        AplicarBotaoTelaCheia(pagina, conteudo);
#endif
        pagina.SetValue(DecoradaProperty, true);
    }

#if WINDOWS
    private static void AplicarBarraWindows(ContentPage pagina, View conteudo)
    {
        var raiz = new Grid();
        raiz.Children.Add(conteudo);

        var minimizar = CriarBotao("—", "Minimizar janela");
        var restaurar = CriarBotao(App.TelaCheia ? "❐" : "□",
            App.TelaCheia ? "Sair da tela cheia" : "Entrar em tela cheia");
        var fechar = CriarBotao("×", "Fechar aplicativo", destrutivo: true);

        var controles = new HorizontalStackLayout
        {
            Spacing = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 2, 3, 0),
            ZIndex = 1000,
            Children = { minimizar, restaurar, fechar }
        };

        minimizar.Clicked += (_, _) =>
        {
            App.MinimizarJanela();
            AtualizarHome(pagina);
        };
        restaurar.Clicked += (_, _) =>
        {
            App.AlternarTelaCheia();
            restaurar.Text = App.TelaCheia ? "❐" : "□";
            SemanticProperties.SetDescription(restaurar,
                App.TelaCheia ? "Sair da tela cheia" : "Entrar em tela cheia");
            AtualizarHome(pagina);
        };
        fechar.Clicked += (_, _) => App.FecharJanela();

        raiz.Children.Add(controles);
        pagina.Content = raiz;
    }

    private static Button CriarBotao(string texto, string descricao, bool destrutivo = false)
    {
        var botao = new Button
        {
            Text = texto,
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0,
            TextColor = Color.FromArgb(destrutivo ? "#DC2626" : "#40506A"),
            CornerRadius = 8,
            FontSize = texto == "×" ? 17 : 13,
            HeightRequest = 27,
            WidthRequest = 32,
            Padding = 0
        };
        SemanticProperties.SetDescription(botao, descricao);
        return botao;
    }
#else
    private static void AplicarBotaoTelaCheia(ContentPage pagina, View conteudo)
    {
        var raiz = new Grid();
        raiz.Children.Add(conteudo);
        var botao = new Button
        {
            Text = App.TelaCheia ? "↙" : "⛶",
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#DDE5EE"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#40506A"),
            CornerRadius = 9,
            FontSize = 18,
            HeightRequest = 44,
            WidthRequest = 44,
            Padding = 0,
            Margin = new Thickness(0, 12, 12, 0),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            ZIndex = 1000
        };
        botao.Clicked += (_, _) =>
        {
            App.AlternarTelaCheia();
            botao.Text = App.TelaCheia ? "↙" : "⛶";
            AtualizarHome(pagina);
        };
        raiz.Children.Add(botao);
        pagina.Content = raiz;
    }
#endif

    private static void AtualizarHome(ContentPage pagina)
    {
        if (pagina is HomePage home)
            home.AtualizarLayoutResponsivo();
    }
}
