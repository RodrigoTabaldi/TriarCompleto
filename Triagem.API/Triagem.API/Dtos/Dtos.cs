namespace Triagem.API.Dtos;

// ---------- Auth ----------
public record RegisterRequest(string Nome, string Email, string Senha);
public record LoginRequest(string Email, string Senha);
public record UsuarioResponse(int Id, string Nome, string Email);

// ---------- Triagem (modelos/templates) ----------
public record PerguntaDto(int Id, string Texto, int Peso, int Ordem);
public record FaixaDto(int Id, string Titulo, string Recomendacao, int PontuacaoMin, int PontuacaoMax, string Cor, int Ordem);

public record TriagemModeloResumo(
    int Id, string Titulo, string PublicoAlvo, string Descricao, string Icone,
    bool Padrao, bool MinhaAutoria, bool VisivelNaHome, int TotalPerguntas);

public record TriagemModeloDetalhe(
    int Id, string Titulo, string PublicoAlvo, string Descricao, string Icone,
    bool Padrao, int? CriadorUsuarioId,
    List<PerguntaDto> Perguntas, List<FaixaDto> Faixas);

public record PerguntaInput(string Texto, int Peso);
public record FaixaInput(string Titulo, string Recomendacao, int PontuacaoMin, int PontuacaoMax, string? Cor);

public record CriarTriagemRequest(
    int UsuarioId, string Titulo, string PublicoAlvo, string? Descricao, string? Icone,
    List<PerguntaInput> Perguntas, List<FaixaInput> Faixas);

// ---------- Home ----------
public record HomeItemInput(int TriagemModeloId, bool Visivel, int Ordem);
public record ConfigurarHomeRequest(List<HomeItemInput> Itens);

// ---------- Execução de triagem ----------
public record RespostaInput(int PerguntaId, bool Valor);
public record ResponderTriagemRequest(
    int UsuarioId, string NomePaciente, int Idade, string Sexo,
    List<RespostaInput> Respostas);

public record ResultadoResponse(
    int Id, int TriagemModeloId, string TituloTriagem,
    string NomePaciente, int Idade, string Sexo,
    int Pontuacao, int PontuacaoMaxima,
    string Classificacao, string Recomendacao, string Cor, DateTime Data);

// ---------- Histórico (formato legado usado pelo app) ----------
public record HistoricoItem(
    int Id, int TriagemModeloId, string TituloTriagem,
    string Nome, int Idade, string Sexo,
    int Pontuacao, int PontuacaoMaxima, string Resultado, string Risco,
    string Cor, DateTime Data);
