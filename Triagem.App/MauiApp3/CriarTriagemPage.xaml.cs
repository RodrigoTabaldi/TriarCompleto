using System.Collections.ObjectModel;
using System.ComponentModel;
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
            var detalhe = await ApiService.ObterTriagemAsync(int.Parse(TriagemId!));
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

            _perguntas.Clear();
            foreach (var p in detalhe.Perguntas.OrderBy(p => p.Ordem))
                AdicionarPerguntaInterna(new PerguntaEditavel { Texto = p.Texto, Peso = p.Peso.ToString() });

            _faixas.Clear();
            foreach (var f in detalhe.Faixas.OrderBy(f => f.Ordem))
                _faixas.Add(new FaixaEditavel
                {
                    Titulo = f.Titulo,
                    Recomendacao = f.Recomendacao,
                    Min = f.PontuacaoMin.ToString(),
                    Max = f.PontuacaoMax.ToString()
                });

            AtualizarPesoTotal();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
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

        _faixas.Add(new FaixaEditavel { Min = min.ToString(), Max = PesoTotal().ToString() });
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

            var payload = new
            {
                usuarioId = usuario.Id,
                titulo = Titulo.Text.Trim(),
                publicoAlvo = PublicoAlvo.Text?.Trim() ?? "",
                descricao = Descricao.Text?.Trim() ?? "",
                icone = "📋",
                perguntas = _perguntas.Select(p => new { texto = p.Texto.Trim(), peso = int.Parse(p.Peso) }),
                faixas = _faixas.Select(f => new
                {
                    titulo = f.Titulo.Trim(),
                    recomendacao = f.Recomendacao?.Trim() ?? "",
                    pontuacaoMin = int.Parse(f.Min),
                    pontuacaoMax = int.Parse(f.Max),
                    cor = (string?)null
                })
            };

            var (ok, erro) = string.IsNullOrEmpty(TriagemId)
                ? await ApiService.CriarTriagemAsync(payload)
                : await ApiService.AtualizarTriagemAsync(int.Parse(TriagemId), payload);

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
