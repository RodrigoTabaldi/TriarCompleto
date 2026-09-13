using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

public partial class TriagemPage : ContentPage, IQueryAttributable
{
    private List<PerguntaRespondivel> _perguntas = [];
    private TriagemDetalhe? _triagem;
    private bool _enviando;
    private bool _carregando;
    private bool _limpando;
    private int _respondidas;

    public string? TriagemId { get; set; }

    public TriagemPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("triagemId", out var id)) TriagemId = id.ToString();
        if (query.TryGetValue("novaPessoa", out var nova) && nova.ToString() == "true")
        {
            query.Remove("novaPessoa");
            Limpar(null, EventArgs.Empty);
            if (_perguntas.Count > 0)
                ListaPerguntas.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_triagem is null)
            await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        if (_carregando) return;
        _carregando = true;
        try
        {
            if (!int.TryParse(TriagemId, out var id))
            {
                await DisplayAlertAsync("Erro", "Triagem inválida.", "OK");
                await Navegacao.IrAsync(this, "..");
                return;
            }

            _triagem = await ApiService.ObterTriagemAsync(id);
            if (_triagem is null)
            {
                await DisplayAlertAsync("Erro", "Triagem não encontrada.", "OK");
                await Navegacao.IrAsync(this, "..");
                return;
            }

            TituloTriagem.Text = _triagem.Titulo;
            ImagemTriagem.Source = TriagemImagem.CriarImageSourceDaTriagem(_triagem.Imagem, _triagem.Titulo);

            foreach (var antiga in _perguntas) antiga.RespostaAlterada -= RespostaAlterada;
            _perguntas = [];
            foreach (var (p, i) in _triagem.Perguntas.OrderBy(p => p.Ordem).Select((p, i) => (p, i)))
            {
                var item = new PerguntaRespondivel
                {
                    PerguntaId = p.Id,
                    Numero = i + 1,
                    Texto = p.Texto,
                    Peso = p.Peso
                };
                item.RespostaAlterada += RespostaAlterada;
                _perguntas.Add(item);
            }

            ListaPerguntas.ItemsSource = _perguntas;
            _respondidas = 0;
            AtualizarProgresso();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro",
                $"Não foi possível carregar a triagem. Verifique se a API está no ar.\n\n{ex.Message}", "OK");
        }
        finally { _carregando = false; }
    }

    private void RespostaAlterada(bool? anterior, bool? atual)
    {
        if (anterior is null && atual is not null) _respondidas++;
        else if (anterior is not null && atual is null) _respondidas--;

        AtualizarProgresso();
    }

    private void AtualizarProgresso()
    {
        if (_limpando) return;
        var total = _perguntas.Count;
        var progresso = total == 0 ? 0 : (double)_respondidas / total;

        ProgressoTexto.Text = $"Pergunta {_respondidas} de {total}";
        ProgressoPercentual.Text = $"{(int)(progresso * 100)}%";
        BarraProgresso.Progress = progresso;
    }

    private void ResponderSim(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is PerguntaRespondivel p)
            p.Resposta = true;
    }

    private void ResponderNao(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is PerguntaRespondivel p)
            p.Resposta = false;
    }

    private void Limpar(object? sender, EventArgs e)
    {
        Nome.Text = "";
        Idade.Text = "";
        Sexo.SelectedIndex = -1;
        _limpando = true;
        foreach (var p in _perguntas) p.Resposta = null;
        _limpando = false;
        _respondidas = 0;
        AtualizarProgresso();
    }

    private async void Finalizar(object? sender, EventArgs e)
    {
        if (_enviando) return;

        try
        {
            if (App.UsuarioLogado is not { } usuario)
            {
                await DisplayAlertAsync("Erro", "Usuário não logado.", "OK");
                return;
            }

            if (_triagem is null) return;

            if (string.IsNullOrWhiteSpace(Nome.Text))
            {
                await DisplayAlertAsync("Atenção", "Informe o nome da pessoa avaliada.", "OK");
                return;
            }

            if (!int.TryParse(Idade.Text, out var idade) || idade < 0 || idade > 130)
            {
                await DisplayAlertAsync("Atenção", "Informe uma idade válida.", "OK");
                return;
            }

            var pendentes = _perguntas.Count(p => p.Resposta is null);
            if (pendentes > 0)
            {
                await DisplayAlertAsync("Atenção",
                    $"Ainda há {pendentes} pergunta(s) sem resposta. Responda todas para finalizar.", "OK");
                return;
            }

            _enviando = true;

            var payload = new ResponderTriagemPayload
            {
                NomePaciente = Nome.Text.Trim(),
                Idade = idade,
                Sexo = Sexo.SelectedItem?.ToString() ?? "",
                Respostas = _perguntas.Select(p => new RespostaTriagemPayload
                {
                    PerguntaId = p.PerguntaId,
                    Valor = p.Resposta == true
                }).ToList()
            };

            var (resultado, erro) = await ApiService.ResponderAsync(_triagem.Id, payload);

            if (resultado is null)
            {
                await DisplayAlertAsync("Erro", erro ?? "Não foi possível finalizar a triagem.", "OK");
                return;
            }

            ResultadoPage.UltimoResultado = resultado;
            await Navegacao.IrAsync(this, $"{nameof(ResultadoPage)}?triagemId={_triagem.Id}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
        finally
        {
            _enviando = false;
        }
    }

    private async void VoltarHome(object? sender, EventArgs e) =>
        await Navegacao.IrAsync(this, "..");
}
