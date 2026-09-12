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

        if (Color.TryParse(r.Cor, out var cor))
        {
            CirculoPontuacao.BackgroundColor = cor;
            Classificacao.TextColor = cor;
        }
    }

    /// <summary>Volta para a mesma triagem, limpa, para aplicar em outra pessoa.</summary>
    private async void RepetirTriagem(object? sender, EventArgs e)
    {
        // Reutiliza as perguntas carregadas, mas reinicia explicitamente o atendimento.
        await Navegacao.IrAsync(this, "..?novaPessoa=true");
    }

    private async void VerHistorico(object? sender, EventArgs e)
    {
        if (UltimoResultado is not { } r) return;
        await Navegacao.IrAsync(this,
            $"{nameof(HistoricoPage)}?triagemId={r.TriagemModeloId}&titulo={Uri.EscapeDataString(r.TituloTriagem)}");
    }

    private async void VoltarHome(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, "../..");
}
