using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

[QueryProperty(nameof(TriagemId), "triagemId")]
public partial class CriarTriagemPage : ContentPage
{
    private readonly ObservableCollection<PerguntaEditavel> _perguntas = [];
    private readonly ObservableCollection<FaixaEditavel> _faixas = [];
    private bool _carregouEdicao;
    private bool _salvando;
    private string? _imagemDataUrl;
    private const int TamanhoMaximoImagem = 2 * 1024 * 1024;

    /// <summary>Quando presente, a página edita uma triagem existente.</summary>
    public string? TriagemId { get; set; }

    public CriarTriagemPage()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(ListaPerguntas, _perguntas);
        BindableLayout.SetItemsSource(ListaFaixas, _faixas);

        _perguntas.CollectionChanged += (_, _) => Renumerar();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(TriagemId) && !_carregouEdicao)
        {
            _carregouEdicao = true;
            await CarregarParaEdicaoAsync();
        }
        else if (_perguntas.Count == 0)
        {
            // começa com uma estrutura mínima de exemplo
            AdicionarPergunta(null, EventArgs.Empty);
            _faixas.Add(new FaixaEditavel { Titulo = "Baixo risco", Min = "0", Max = "0", Recomendacao = "Sem sinais de alerta no momento." });
            _faixas.Add(new FaixaEditavel { Titulo = "Alto risco", Min = "1", Max = "1", Recomendacao = "Procure uma avaliação profissional." });
        }
    }

    private async Task CarregarParaEdicaoAsync()
    {
        try
        {
            var detalhe = await ApiService.ObterTriagemAsync(int.Parse(TriagemId!, CultureInfo.InvariantCulture));
            if (detalhe is null)
            {
                await DisplayAlertAsync("Erro", "Triagem não encontrada.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            TituloPagina.Text = "Editar triagem";
            BotaoSalvar.Text = "Salvar alterações ✓";
            Titulo.Text = detalhe.Titulo;
            PublicoAlvo.Text = detalhe.PublicoAlvo;
            Descricao.Text = detalhe.Descricao;
            _imagemDataUrl = detalhe.Imagem;
            AtualizarPreviewImagem();

            _perguntas.Clear();
            foreach (var p in detalhe.Perguntas.OrderBy(p => p.Ordem))
                AdicionarPerguntaInterna(new PerguntaEditavel { Texto = p.Texto, Peso = p.Peso.ToString(CultureInfo.InvariantCulture) });

            _faixas.Clear();
            foreach (var f in detalhe.Faixas.OrderBy(f => f.Ordem))
                _faixas.Add(new FaixaEditavel
                {
                    Titulo = f.Titulo,
                    Recomendacao = f.Recomendacao,
                    Min = f.PontuacaoMin.ToString(CultureInfo.InvariantCulture),
                    Max = f.PontuacaoMax.ToString(CultureInfo.InvariantCulture)
                });

            AtualizarPesoTotal();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }

    // ---------------- Imagem ilustrativa ----------------

    private async void EscolherImagem(object? sender, EventArgs e)
    {
        try
        {
            var arquivo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Escolha a imagem da triagem",
                FileTypes = FilePickerFileType.Images
            });

            if (arquivo is null) return;

            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            var mime = extensao switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => null
            };

            if (mime is null)
            {
                await DisplayAlertAsync("Formato não suportado", "Escolha uma imagem PNG, JPG ou WebP.", "OK");
                return;
            }

            await using var stream = await arquivo.OpenReadAsync();
            if (stream.CanSeek && stream.Length > TamanhoMaximoImagem)
            {
                await DisplayAlertAsync("Imagem muito grande", "Escolha uma imagem de até 2 MB.", "OK");
                return;
            }

            var bytes = await LerImagemComLimiteAsync(stream);
            if (bytes is null)
            {
                await DisplayAlertAsync("Imagem muito grande", "Escolha uma imagem de até 2 MB.", "OK");
                return;
            }

            _imagemDataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            PreviewImagem.Source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
            BotaoEscolherImagem.Text = "Trocar imagem";
            BotaoRemoverImagem.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", $"Não foi possível abrir a imagem: {ex.Message}", "OK");
        }
    }

    private static async Task<byte[]?> LerImagemComLimiteAsync(Stream stream)
    {
        using var memoria = new MemoryStream(capacity: TamanhoMaximoImagem);
        var buffer = new byte[64 * 1024];

        while (true)
        {
            var lidos = await stream.ReadAsync(buffer);
            if (lidos == 0) break;
            if (memoria.Length + lidos > TamanhoMaximoImagem) return null;
            await memoria.WriteAsync(buffer.AsMemory(0, lidos));
        }

        return memoria.ToArray();
    }

    private void RemoverImagem(object? sender, EventArgs e)
    {
        _imagemDataUrl = null;
        AtualizarPreviewImagem();
    }

    private void AtualizarPreviewImagem()
    {
        PreviewImagem.Source = TriagemImagem.CriarImageSource(_imagemDataUrl, "triagem_clinica_profissional.png");
        BotaoEscolherImagem.Text = string.IsNullOrWhiteSpace(_imagemDataUrl) ? "Escolher imagem" : "Trocar imagem";
        BotaoRemoverImagem.IsVisible = !string.IsNullOrWhiteSpace(_imagemDataUrl);
    }

    // ---------------- Perguntas ----------------

    private void AdicionarPergunta(object? sender, EventArgs e) =>
        AdicionarPerguntaInterna(new PerguntaEditavel());

    private void AdicionarPerguntaInterna(PerguntaEditavel pergunta)
    {
        pergunta.PropertyChanged += PesoAlterado;
        _perguntas.Add(pergunta);
        AtualizarPesoTotal();
    }

    private void RemoverPergunta(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is PerguntaEditavel p)
        {
            p.PropertyChanged -= PesoAlterado;
            _perguntas.Remove(p);
            AtualizarPesoTotal();
        }
    }

    private void PesoAlterado(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PerguntaEditavel.Peso))
            AtualizarPesoTotal();
    }

    private void Renumerar()
    {
        for (var i = 0; i < _perguntas.Count; i++)
            _perguntas[i].Numero = i + 1;
    }

    private void AtualizarPesoTotal() =>
        PesoTotalLabel.Text = $"Pontuação máxima possível: {PesoTotal()}";

    private int PesoTotal() =>
        _perguntas.Sum(p => int.TryParse(p.Peso, out var peso) ? peso : 0);

    // ---------------- Faixas ----------------

    private void AdicionarFaixa(object? sender, EventArgs e)
    {
        var min = _faixas.Count == 0
            ? 0
            : (int.TryParse(_faixas[^1].Max, out var maxAnterior) ? maxAnterior + 1 : 0);

        _faixas.Add(new FaixaEditavel { Min = min.ToString(CultureInfo.InvariantCulture), Max = PesoTotal().ToString(CultureInfo.InvariantCulture) });
    }

    private void RemoverFaixa(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is FaixaEditavel f)
            _faixas.Remove(f);
    }

    // ---------------- Salvar ----------------

    private async void Salvar(object? sender, EventArgs e)
    {
        if (_salvando) return;

        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await DisplayAlertAsync("Erro", "Usuário não logado.", "OK");
                return;
            }

            // validações locais (a API valida novamente)
            if (string.IsNullOrWhiteSpace(Titulo.Text))
            {
                await DisplayAlertAsync("Atenção", "Informe o título da triagem.", "OK");
                return;
            }

            if (_perguntas.Count == 0 || _perguntas.Any(p => string.IsNullOrWhiteSpace(p.Texto)))
            {
                await DisplayAlertAsync("Atenção", "Adicione ao menos uma pergunta e preencha o texto de todas.", "OK");
                return;
            }

            if (_perguntas.Any(p => !int.TryParse(p.Peso, out var peso) || peso < 1))
            {
                await DisplayAlertAsync("Atenção", "Todo peso deve ser um número inteiro maior ou igual a 1.", "OK");
                return;
            }

            if (_faixas.Count < 2)
            {
                await DisplayAlertAsync("Atenção", "Defina pelo menos duas faixas de resultado.", "OK");
                return;
            }

            foreach (var f in _faixas)
            {
                if (string.IsNullOrWhiteSpace(f.Titulo) ||
                    !int.TryParse(f.Min, out _) || !int.TryParse(f.Max, out _))
                {
                    await DisplayAlertAsync("Atenção", "Preencha título e pontuações (mínima e máxima) de todas as faixas.", "OK");
                    return;
                }
            }

            _salvando = true;

            var payload = new CriarTriagemPayload
            {
                Titulo = Titulo.Text.Trim(),
                PublicoAlvo = PublicoAlvo.Text?.Trim() ?? "",
                Descricao = Descricao.Text?.Trim() ?? "",
                Icone = "📋",
                Imagem = _imagemDataUrl,
                Perguntas = _perguntas.Select(p => new PerguntaTriagemPayload
                {
                    Texto = p.Texto.Trim(), Peso = int.Parse(p.Peso, CultureInfo.InvariantCulture)
                }).ToList(),
                Faixas = _faixas.Select(f => new FaixaTriagemPayload
                {
                    Titulo = f.Titulo.Trim(),
                    Recomendacao = f.Recomendacao?.Trim() ?? "",
                    PontuacaoMin = int.Parse(f.Min, CultureInfo.InvariantCulture),
                    PontuacaoMax = int.Parse(f.Max, CultureInfo.InvariantCulture),
                    Cor = null
                }).ToList()
            };

            var (ok, erro) = string.IsNullOrEmpty(TriagemId)
                ? await ApiService.CriarTriagemAsync(payload)
                : await ApiService.AtualizarTriagemAsync(int.Parse(TriagemId, CultureInfo.InvariantCulture), payload);

            if (!ok)
            {
                await DisplayAlertAsync("Erro", erro ?? "Não foi possível salvar a triagem.", "OK");
                return;
            }

            await DisplayAlertAsync("Sucesso",
                "Triagem salva! Ela já está disponível na sua home.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
        finally
        {
            _salvando = false;
        }
    }

    private async void Voltar(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
