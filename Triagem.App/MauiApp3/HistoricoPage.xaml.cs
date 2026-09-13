using ClosedXML.Excel;
using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

[QueryProperty(nameof(TriagemId), "triagemId")]
[QueryProperty(nameof(Titulo), "titulo")]
public partial class HistoricoPage : ContentPage
{
    private List<HistoricoItem> _itens = [];
    private bool _carregando;
    private bool _exportando;
    private bool _carregado;
    private int _versaoCarregada = -1;

    /// <summary>Opcional: filtra o histórico por uma triagem específica.</summary>
    public string? TriagemId { get; set; }
    public string? Titulo { get; set; }

    public HistoricoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(Titulo))
            Subtitulo.Text = Uri.UnescapeDataString(Titulo);

        if (!_carregado || _versaoCarregada != ApiService.VersaoHistorico)
            await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        if (_carregando) return;
        _carregando = true;
        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await DisplayAlertAsync("Erro", "Usuário não logado.", "OK");
                await Navegacao.IrAsync(this, "..");
                return;
            }

            int? triagemId = int.TryParse(TriagemId, out var id) ? id : null;
            _itens = await ApiService.HistoricoAsync(usuario.Id, triagemId);
            Lista.ItemsSource = _itens;
            _carregado = true;
            _versaoCarregada = ApiService.VersaoHistorico;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro",
                $"Não foi possível carregar o histórico.\n\n{ex.Message}", "OK");
        }
        finally { _carregando = false; }
    }

    private async void ExportarExcel(object? sender, EventArgs e)
    {
        if (_exportando) return;
        _exportando = true;
        string? caminhoTemporario = null;
        try
        {
            if (_itens.Count == 0)
            {
                await DisplayAlertAsync("Atenção", "Não há triagens para exportar.", "OK");
                return;
            }

            var itens = _itens.ToArray();
            caminhoTemporario = Path.Combine(FileSystem.Current.AppDataDirectory,
                $"Triagens_{Guid.NewGuid():N}.xlsx");
            await Task.Run(() =>
            {
            using var workbook = new XLWorkbook();
            var planilha = workbook.Worksheets.Add("Triagens");

            string[] cabecalho = ["Triagem", "Nome", "Idade", "Sexo", "Pontuação", "Máximo", "Resultado", "Data"];
            for (var c = 0; c < cabecalho.Length; c++)
                planilha.Cell(1, c + 1).Value = cabecalho[c];

            var header = planilha.Range(1, 1, 1, cabecalho.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.Green;
            header.Style.Font.FontColor = XLColor.White;

            var linha = 2;
            foreach (var item in itens)
            {
                planilha.Cell(linha, 1).Value = item.TituloTriagem;
                planilha.Cell(linha, 2).Value = item.Nome;
                planilha.Cell(linha, 3).Value = item.Idade;
                planilha.Cell(linha, 4).Value = item.Sexo;
                planilha.Cell(linha, 5).Value = item.Pontuacao;
                planilha.Cell(linha, 6).Value = item.PontuacaoMaxima;
                planilha.Cell(linha, 7).Value = item.Resultado;
                planilha.Cell(linha, 8).Value = item.DataFormatada;
                linha++;
            }

            planilha.Columns().AdjustToContents();

            workbook.SaveAs(caminhoTemporario);
            });

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Compartilhar Excel",
                File = new ShareFile(caminhoTemporario)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
        finally
        {
            _exportando = false;
            if (!string.IsNullOrWhiteSpace(caminhoTemporario) && File.Exists(caminhoTemporario))
            {
                try { File.Delete(caminhoTemporario); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private async void Voltar(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, "..");
}
