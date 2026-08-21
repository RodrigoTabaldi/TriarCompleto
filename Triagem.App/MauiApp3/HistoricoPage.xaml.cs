using System.Collections.ObjectModel;
using ClosedXML.Excel;
using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

[QueryProperty(nameof(TriagemId), "triagemId")]
[QueryProperty(nameof(Titulo), "titulo")]
public partial class HistoricoPage : ContentPage
{
    private readonly ObservableCollection<HistoricoItem> _itens = [];
    private int _paginaCarregada;
    private bool _temMaisPaginas = true;
    private bool _carregandoMais;

    /// <summary>Opcional: filtra o histórico por uma triagem específica.</summary>
    public string? TriagemId { get; set; }
    public string? Titulo { get; set; }

    public HistoricoPage()
    {
        InitializeComponent();
        Lista.ItemsSource = _itens;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(Titulo))
            Subtitulo.Text = Uri.UnescapeDataString(Titulo);

        await CarregarAsync();
    }

    private int? TriagemIdFiltro => int.TryParse(TriagemId, out var id) ? id : null;

    private async Task CarregarAsync()
    {
        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await DisplayAlertAsync("Erro", "Usuário não logado.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            var primeiraPagina = await ApiService.HistoricoAsync(usuario.Id, TriagemIdFiltro, pagina: 1);

            _itens.Clear();
            foreach (var item in primeiraPagina) _itens.Add(item);

            _paginaCarregada = 1;
            _temMaisPaginas = primeiraPagina.Count >= ApiService.TamanhoPaginaHistorico;
        }
        catch (Exception ex)
        {
            if (ApiService.EhSessaoExpirada(ex))
            {
                await TratarSessaoExpiradaAsync();
                return;
            }

            await DisplayAlertAsync("Erro",
                $"Não foi possível carregar o histórico.\n\n{ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Carrega a próxima página quando a rolagem se aproxima do fim da lista (ver
    /// RemainingItemsThreshold no XAML) — evita carregar o histórico inteiro de uma
    /// vez, mas também evita truncar silenciosamente em 100 registros como antes.
    /// </summary>
    private async void CarregarProximaPagina(object? sender, EventArgs e)
    {
        if (_carregandoMais || !_temMaisPaginas) return;
        if (App.UsuarioLogado is not { } usuario) return;

        _carregandoMais = true;
        IndicadorCarregandoMais.IsVisible = true;
        IndicadorCarregandoMais.IsRunning = true;
        try
        {
            var proximaPagina = await ApiService.HistoricoAsync(usuario.Id, TriagemIdFiltro, _paginaCarregada + 1);

            foreach (var item in proximaPagina) _itens.Add(item);

            _paginaCarregada++;
            _temMaisPaginas = proximaPagina.Count >= ApiService.TamanhoPaginaHistorico;
        }
        catch (Exception ex)
        {
            if (ApiService.EhSessaoExpirada(ex))
            {
                await TratarSessaoExpiradaAsync();
                return;
            }
            // Falha ao carregar mais não é crítica (a página já mostra o que tinha):
            // tenta de novo na próxima vez que a rolagem alcançar o limiar.
        }
        finally
        {
            _carregandoMais = false;
            IndicadorCarregandoMais.IsVisible = false;
            IndicadorCarregandoMais.IsRunning = false;
        }
    }

    private async Task TratarSessaoExpiradaAsync()
    {
        ApiService.Logout();
        App.UsuarioLogado = null;
        await DisplayAlertAsync("Sessão expirada", "Faça login novamente para continuar.", "OK");
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }

    private async void ExportarExcel(object? sender, EventArgs e)
    {
        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await DisplayAlertAsync("Erro", "Usuário não logado.", "OK");
                return;
            }

            // Busca todas as páginas para exportar: a lista na tela pode ter carregado
            // só uma parte do histórico até agora (rolagem incremental).
            var todosOsItens = await ApiService.HistoricoCompletoAsync(usuario.Id, TriagemIdFiltro);

            if (todosOsItens.Count == 0)
            {
                await DisplayAlertAsync("Atenção", "Não há triagens para exportar.", "OK");
                return;
            }

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
            foreach (var item in todosOsItens)
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

            // Remove exportações anteriores antes de gravar a nova: sem isto, cada
            // exportação deixava permanentemente no aparelho uma planilha com nomes de
            // pacientes e classificações de risco em texto plano, acumulando sem limite.
            LimparExportacoesAnteriores();

            var caminho = Path.Combine(
                FileSystem.Current.AppDataDirectory,
                $"Triagens_{DateTime.Now:ddMMyyyyHHmmss}.xlsx");

            workbook.SaveAs(caminho);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Compartilhar Excel",
                File = new ShareFile(caminho)
            });
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

    private static void LimparExportacoesAnteriores()
    {
        try
        {
            foreach (var arquivo in Directory.EnumerateFiles(FileSystem.Current.AppDataDirectory, "Triagens_*.xlsx"))
                File.Delete(arquivo);
        }
        catch (IOException)
        {
            // Arquivo pode estar aberto/sendo compartilhado ainda (ex.: usuário exportou
            // duas vezes seguidas rápido) — não é motivo para impedir a nova exportação.
        }
    }

    private async void Voltar(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
