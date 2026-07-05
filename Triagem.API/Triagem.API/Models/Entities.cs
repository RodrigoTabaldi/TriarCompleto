namespace Triagem.API.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<TriagemModelo> TriagensCriadas { get; set; } = [];
    public List<UsuarioTriagemHome> ConfiguracaoHome { get; set; } = [];
}

/// <summary>
/// Modelo (template) de uma triagem: conjunto de perguntas sim/não com pesos
/// e faixas de pontuação que definem o resultado.
/// </summary>
public class TriagemModelo
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string PublicoAlvo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Icone { get; set; } = "🩺";

    /// <summary>Null = triagem padrão do sistema; senão, id do usuário criador.</summary>
    public int? CriadorUsuarioId { get; set; }
    public Usuario? CriadorUsuario { get; set; }

    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<Pergunta> Perguntas { get; set; } = [];
    public List<FaixaResultado> Faixas { get; set; } = [];
}

public class Pergunta
{
    public int Id { get; set; }
    public int TriagemModeloId { get; set; }
    public TriagemModelo? TriagemModelo { get; set; }

    public string Texto { get; set; } = "";
    /// <summary>Peso somado à pontuação quando a resposta é "Sim".</summary>
    public int Peso { get; set; } = 1;
    public int Ordem { get; set; }
}

/// <summary>
/// Faixa de pontuação que define o resultado/meta da triagem
/// (ex.: 0-3 Baixo risco, 4-7 Risco moderado, 8+ Alto risco).
/// </summary>
public class FaixaResultado
{
    public int Id { get; set; }
    public int TriagemModeloId { get; set; }
    public TriagemModelo? TriagemModelo { get; set; }

    public string Titulo { get; set; } = "";
    public string Recomendacao { get; set; } = "";
    public int PontuacaoMin { get; set; }
    public int PontuacaoMax { get; set; }
    /// <summary>Cor de destaque no app (hex).</summary>
    public string Cor { get; set; } = "#10B981";
    public int Ordem { get; set; }
}

/// <summary>Preferência do usuário sobre quais triagens aparecem na home.</summary>
public class UsuarioTriagemHome
{
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public int TriagemModeloId { get; set; }
    public TriagemModelo? TriagemModelo { get; set; }
    public bool Visivel { get; set; } = true;
    public int Ordem { get; set; }
}

/// <summary>Uma aplicação (execução) de uma triagem em uma pessoa.</summary>
public class TriagemResultado
{
    public int Id { get; set; }
    public int TriagemModeloId { get; set; }
    public TriagemModelo? TriagemModelo { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string NomePaciente { get; set; } = "";
    public int Idade { get; set; }
    public string Sexo { get; set; } = "";

    public int Pontuacao { get; set; }
    public int PontuacaoMaxima { get; set; }
    public string Classificacao { get; set; } = "";
    public string Recomendacao { get; set; } = "";
    public string Cor { get; set; } = "#10B981";
    public DateTime Data { get; set; } = DateTime.UtcNow;

    public List<RespostaDada> Respostas { get; set; } = [];
}

public class RespostaDada
{
    public int Id { get; set; }
    public int TriagemResultadoId { get; set; }
    public TriagemResultado? TriagemResultado { get; set; }
    public int PerguntaId { get; set; }
    public bool Valor { get; set; }
}
