using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiApp3.Models;

public class Usuario
{
    public int Id { get; set; }
    public string? Nome { get; set; }
    public string? Email { get; set; }
}

// ---------- Triagens (modelos) ----------

public class TriagemResumo : ObservableBase
{
    private bool _visivelNaHome = true;
    private bool _modoEdicao;

    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string PublicoAlvo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Icone { get; set; } = "🩺";
    public bool Padrao { get; set; }
    public bool MinhaAutoria { get; set; }
    public int TotalPerguntas { get; set; }

    public bool VisivelNaHome { get => _visivelNaHome; set => Set(ref _visivelNaHome, value); }

    /// <summary>Controlado pela HomePage: alterna entre exibir os botões ou o switch de visibilidade.</summary>
    public bool ModoEdicao
    {
        get => _modoEdicao;
        set { if (Set(ref _modoEdicao, value)) Notificar(nameof(ModoNormal)); }
    }
    public bool ModoNormal => !ModoEdicao;
}

public class PerguntaDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = "";
    public int Peso { get; set; }
    public int Ordem { get; set; }
}

public class FaixaDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Recomendacao { get; set; } = "";
    public int PontuacaoMin { get; set; }
    public int PontuacaoMax { get; set; }
    public string Cor { get; set; } = "#10B981";
    public int Ordem { get; set; }
}

public class TriagemDetalhe
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string PublicoAlvo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Icone { get; set; } = "🩺";
    public bool Padrao { get; set; }
    public int? CriadorUsuarioId { get; set; }
    public List<PerguntaDto> Perguntas { get; set; } = [];
    public List<FaixaDto> Faixas { get; set; } = [];
}

// ---------- Execução / resultado ----------

public class ResultadoTriagem
{
    public int Id { get; set; }
    public int TriagemModeloId { get; set; }
    public string TituloTriagem { get; set; } = "";
    public string NomePaciente { get; set; } = "";
    public int Idade { get; set; }
    public string Sexo { get; set; } = "";
    public int Pontuacao { get; set; }
    public int PontuacaoMaxima { get; set; }
    public string Classificacao { get; set; } = "";
    public string Recomendacao { get; set; } = "";
    public string Cor { get; set; } = "#10B981";
    public DateTime Data { get; set; }
}

public class HistoricoItem
{
    public int Id { get; set; }
    public int TriagemModeloId { get; set; }
    public string TituloTriagem { get; set; } = "";
    public string Nome { get; set; } = "";
    public int Idade { get; set; }
    public string Sexo { get; set; } = "";
    public int Pontuacao { get; set; }
    public int PontuacaoMaxima { get; set; }
    public string Resultado { get; set; } = "";
    public string Cor { get; set; } = "#10B981";
    public DateTime Data { get; set; }

    public string DataFormatada => Data.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string PontuacaoFormatada => $"{Pontuacao}/{PontuacaoMaxima}";
}

// ---------- Modelos editáveis (Criar/Editar triagem) ----------

public class ObservableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T campo, T valor, [CallerMemberName] string? nome = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
        campo = valor;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
        return true;
    }

    protected void Notificar(string nome) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
}

public class PerguntaEditavel : ObservableBase
{
    private string _texto = "";
    private string _peso = "1";
    private int _numero;

    public string Texto { get => _texto; set => Set(ref _texto, value); }
    public string Peso { get => _peso; set => Set(ref _peso, value); }
    public int Numero { get => _numero; set => Set(ref _numero, value); }
}

public class FaixaEditavel : ObservableBase
{
    private string _titulo = "";
    private string _recomendacao = "";
    private string _min = "0";
    private string _max = "0";

    public string Titulo { get => _titulo; set => Set(ref _titulo, value); }
    public string Recomendacao { get => _recomendacao; set => Set(ref _recomendacao, value); }
    public string Min { get => _min; set => Set(ref _min, value); }
    public string Max { get => _max; set => Set(ref _max, value); }
}

public class HomeConfigItem : ObservableBase
{
    private bool _visivel;

    public int TriagemModeloId { get; set; }
    public string Titulo { get; set; } = "";
    public string Icone { get; set; } = "🩺";
    public bool Visivel { get => _visivel; set => Set(ref _visivel, value); }
}

// ---------- Item respondível (execução da triagem) ----------

public class PerguntaRespondivel : ObservableBase
{
    private bool? _resposta;

    public int PerguntaId { get; set; }
    public int Numero { get; set; }
    public string Texto { get; set; } = "";
    public int Peso { get; set; }

    public bool? Resposta
    {
        get => _resposta;
        set
        {
            if (Set(ref _resposta, value))
            {
                Notificar(nameof(SimSelecionado));
                Notificar(nameof(NaoSelecionado));
                RespostaAlterada?.Invoke();
            }
        }
    }

    public bool SimSelecionado => Resposta == true;
    public bool NaoSelecionado => Resposta == false;

    public event Action? RespostaAlterada;
}
