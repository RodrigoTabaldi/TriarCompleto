using System.Collections.ObjectModel;
using MauiApp3.Models;
using MauiApp3.Services;

namespace MauiApp3;

[QueryProperty(nameof(TriagemId), "triagemId")]
public partial class TriagemPage : ContentPage
{
    private readonly ObservableCollection<PerguntaRespondivel> _perguntas = [];
    private TriagemDetalhe? _triagem;
    private bool _enviando;

    public string? TriagemId { get; set; }

    public TriagemPage()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(ListaPerguntas, _perguntas);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_triagem is null)
            await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            if (!int.TryParse(TriagemId, out var id))
            {
                await DisplayAlertAsync("Erro", "Triagem inválida.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            _triagem = await ApiService.ObterTriagemAsync(id);
            if (_triagem is null)
            {
                await DisplayAlertAsync("Erro", "Triagem não encontrada.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            TituloTriagem.Text = _triagem.Titulo;
            IconeTriagem.Text = _triagem.Icone;

            _perguntas.Clear();
            foreach (var (p, i) in _triagem.Perguntas.OrderBy(p => p.Ordem).Select((p, i) => (p, i)))
            {
                var item = new PerguntaRespondivel
                {
                    PerguntaId = p.Id,
                    Numero = i + 1,
                    Texto = p.Texto,
                    Peso = p.Peso
                };
                item.RespostaAlterada += AtualizarProgresso;
                _perguntas.Add(item);
            }

            AtualizarProgresso();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro",
                $"Não foi possível carregar a triagem. Verifique se a API está no ar.\n\n{ex.Message}", "OK");
        }
    }

    private void AtualizarProgresso()
    {
        var total = _perguntas.Count;
        var respondidas = _perguntas.Count(p => p.Resposta is not null);
        var progresso = total == 0 ? 0 : (double)respondidas / total;

        ProgressoTexto.Text = $"Pergunta {respondidas} de {total}";
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
        foreach (var p in _perguntas) p.Resposta = null;
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

            var payload = new
            {
                usuarioId = usuario.Id,
                nomePaciente = Nome.Text.Trim(),
                idade,
                sexo = Sexo.SelectedItem?.ToString() ?? "",
                respostas = _perguntas.Select(p => new { perguntaId = p.PerguntaId, valor = p.Resposta == true })
            };

            var (resultado, erro) = await ApiService.ResponderAsync(_triagem.Id, payload);

            if (resultado is null)
            {
                await DisplayAlertAsync("Erro", erro ?? "Não foi possível finalizar a triagem.", "OK");
                return;
            }

            ResultadoPage.UltimoResultado = resultado;
            await Shell.Current.GoToAsync($"{nameof(ResultadoPage)}?triagemId={_triagem.Id}");
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
        await Shell.Current.GoToAsync("..");
}
