using System.Globalization;
using MauiApp3.Models;

namespace MauiApp3;

[QueryProperty(nameof(TriagemId), "triagemId")]
public partial class ResultadoPage : ContentPage
{
    /// <summary>Resultado calculado pela TriagemPage, exibido nesta tela.</summary>
    public static ResultadoTriagem? UltimoResultado { get; set; }

    public string? TriagemId { get; set; }

    public ResultadoPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (UltimoResultado is not { } r) return;

        TituloTriagem.Text = r.TituloTriagem;
        Pontuacao.Text = r.Pontuacao.ToString(CultureInfo.InvariantCulture);
        PontuacaoMaxima.Text = $"de {r.PontuacaoMaxima} pontos";
        Classificacao.Text = r.Classificacao;
        Recomendacao.Text = r.Recomendacao;
        NomePaciente.Text = r.NomePaciente;
        IdadePaciente.Text = $"{r.Idade} anos";
        DataTriagem.Text = r.Data.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        DetalhesRespostas.IsVisible = r.Respostas.Count > 0;
        ListaRespostas.Children.Clear();
        foreach (var resposta in r.Respostas.OrderBy(x => x.Numero))
        {
            ListaRespostas.Children.Add(new Border
            {
                BackgroundColor = Colors.White,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
                Padding = new Thickness(14),
                Content = new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label { Text = $"{resposta.Numero}. {resposta.Pergunta}", FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0F172A") },
                        new Label { Text = resposta.Resposta, TextColor = Color.FromArgb("#374151") }
                    }
                }
            });
        }

        if (Color.TryParse(r.Cor, out var cor))
        {
            CirculoPontuacao.BackgroundColor = cor;
            Classificacao.TextColor = cor;
        }
    }

    /// <summary>Volta para a mesma triagem, limpa, para aplicar em outra pessoa.</summary>
    private async void RepetirTriagem(object? sender, EventArgs e)
    {
        var id = UltimoResultado?.TriagemModeloId.ToString(CultureInfo.InvariantCulture) ?? TriagemId;
        // remove a TriagemPage anterior da pilha e abre uma nova em branco
        await Shell.Current.GoToAsync($"../..");
        await Shell.Current.GoToAsync($"{nameof(TriagemPage)}?triagemId={id}");
    }

    private async void VoltarHome(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("../..");
}
