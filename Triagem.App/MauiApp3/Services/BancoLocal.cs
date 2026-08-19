using System.Security.Cryptography;
using System.Text.Json;
using MauiApp3.Models;
using SQLite;

namespace MauiApp3.Services;

/// <summary>
/// Banco de dados local (SQLite, no próprio dispositivo) usado quando o app roda em
/// <see cref="ApiService.ModoLocal"/> — ou seja, sem API, sem rede e sem servidor.
///
/// É uma reimplementação enxuta das mesmas regras de negócio da Triagem.API
/// (cadastro/login, catálogo de triagens, execução com pesos e faixas, histórico e
/// configuração da home), para que o APK de demonstração funcione sozinho. O contrato
/// exposto aqui é o mesmo que o <see cref="ApiService"/> usa contra a API real, então
/// nenhuma tela precisa saber em qual dos dois modos está rodando.
/// </summary>
public static partial class BancoLocal
{
    public const string ArquivoBanco = "triar-local.db3";

    // Conta pronta para a demonstração — criada no primeiro uso junto com as
    // triagens padrão. Continua sendo possível cadastrar outras contas normalmente.
    public const string EmailDemo = "demo@triar.com";
    public const string SenhaDemo = "triar1234";

    private static SQLiteAsyncConnection? _conexao;
    private static readonly SemaphoreSlim Inicializacao = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string CaminhoBanco => Path.Combine(FileSystem.AppDataDirectory, ArquivoBanco);

    private static async Task<SQLiteAsyncConnection> ConexaoAsync()
    {
        if (_conexao is not null) return _conexao;

        await Inicializacao.WaitAsync();
        try
        {
            if (_conexao is not null) return _conexao;

            var conexao = new SQLiteAsyncConnection(CaminhoBanco,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

            await conexao.CreateTableAsync<UsuarioLocal>();
            await conexao.CreateTableAsync<TriagemModeloLocal>();
            await conexao.CreateTableAsync<PerguntaLocal>();
            await conexao.CreateTableAsync<FaixaLocal>();
            await conexao.CreateTableAsync<HomePrefLocal>();
            await conexao.CreateTableAsync<ResultadoLocal>();
            await conexao.CreateTableAsync<RespostaLocal>();

            await SemearAsync(conexao);

            _conexao = conexao;
            return conexao;
        }
        finally
        {
            Inicializacao.Release();
        }
    }

    // ---------------- Autenticação ----------------

    private const int SenhaMinima = 8;

    public static async Task<(Usuario? Usuario, string? Erro)> RegistrarAsync(string nome, string email, string senha)
    {
        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            return (null, "Preencha nome, email e senha.");

        if (senha.Length < SenhaMinima)
            return (null, $"A senha deve ter pelo menos {SenhaMinima} caracteres.");

        var db = await ConexaoAsync();
        var emailNormalizado = email.Trim().ToLowerInvariant();

        var jaExiste = await db.Table<UsuarioLocal>().Where(u => u.Email == emailNormalizado).FirstOrDefaultAsync();
        if (jaExiste is not null)
            return (null, "Já existe uma conta com este email.");

        var usuario = new UsuarioLocal
        {
            Nome = nome.Trim(),
            Email = emailNormalizado,
            SenhaHash = HashSenha(senha)
        };
        await db.InsertAsync(usuario);

        return (new Usuario { Id = usuario.Id, Nome = usuario.Nome, Email = usuario.Email }, null);
    }

    public static async Task<Usuario?> LoginAsync(string email, string senha)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) return null;

        var db = await ConexaoAsync();
        var emailNormalizado = email.Trim().ToLowerInvariant();

        var usuario = await db.Table<UsuarioLocal>().Where(u => u.Email == emailNormalizado).FirstOrDefaultAsync();
        if (usuario is null || !VerificarSenha(senha, usuario.SenhaHash)) return null;

        return new Usuario { Id = usuario.Id, Nome = usuario.Nome, Email = usuario.Email };
    }

    /// <summary>
    /// Confere se o usuário de uma sessão restaurada ainda existe no banco do
    /// dispositivo. Sem isso, uma sessão salva no armazenamento seguro poderia
    /// sobreviver à limpeza dos dados do app e apontar para um usuário inexistente.
    /// </summary>
    public static async Task<bool> UsuarioExisteAsync(int usuarioId)
    {
        var db = await ConexaoAsync();
        return await db.Table<UsuarioLocal>().Where(u => u.Id == usuarioId).FirstOrDefaultAsync() is not null;
    }

    // ---------------- Catálogo de triagens ----------------

    public static async Task<List<TriagemResumo>> ListarTriagensAsync(int usuarioId)
    {
        var db = await ConexaoAsync();

        var modelos = (await db.Table<TriagemModeloLocal>()
                .Where(t => t.Ativa && (t.CriadorUsuarioId == null || t.CriadorUsuarioId == usuarioId))
                .ToListAsync())
            .OrderBy(t => t.CriadorUsuarioId == null ? 0 : 1)
            .ThenBy(t => t.Id)
            .ToList();

        var perguntasPorModelo = (await db.Table<PerguntaLocal>().ToListAsync())
            .GroupBy(p => p.TriagemModeloId)
            .ToDictionary(g => g.Key, g => g.Count());

        var preferencias = (await db.Table<HomePrefLocal>().Where(h => h.UsuarioId == usuarioId).ToListAsync())
            .ToDictionary(h => h.TriagemModeloId);

        return modelos.Select(t => new TriagemResumo
        {
            Id = t.Id,
            Titulo = t.Titulo,
            PublicoAlvo = t.PublicoAlvo,
            Descricao = t.Descricao,
            Icone = t.Icone,
            Padrao = t.CriadorUsuarioId is null,
            MinhaAutoria = t.CriadorUsuarioId == usuarioId,
            VisivelNaHome = !preferencias.TryGetValue(t.Id, out var pref) || pref.Visivel,
            TotalPerguntas = perguntasPorModelo.TryGetValue(t.Id, out var total) ? total : 0
        }).ToList();
    }

    /// <summary>
    /// Detalhe de uma triagem. Só devolve triagens padrão do sistema ou criadas pelo
    /// próprio usuário — mesma regra da API, para que o comportamento do app seja
    /// idêntico nos dois modos.
    /// </summary>
    public static async Task<TriagemDetalhe?> ObterTriagemAsync(int usuarioId, int id)
    {
        var db = await ConexaoAsync();

        var modelo = await db.Table<TriagemModeloLocal>()
            .Where(t => t.Id == id && t.Ativa && (t.CriadorUsuarioId == null || t.CriadorUsuarioId == usuarioId))
            .FirstOrDefaultAsync();

        if (modelo is null) return null;

        var perguntas = (await db.Table<PerguntaLocal>().Where(p => p.TriagemModeloId == id).ToListAsync())
            .OrderBy(p => p.Ordem).ToList();
        var faixas = (await db.Table<FaixaLocal>().Where(f => f.TriagemModeloId == id).ToListAsync())
            .OrderBy(f => f.Ordem).ToList();

        return new TriagemDetalhe
        {
            Id = modelo.Id,
            Titulo = modelo.Titulo,
            PublicoAlvo = modelo.PublicoAlvo,
            Descricao = modelo.Descricao,
            Icone = modelo.Icone,
            Padrao = modelo.CriadorUsuarioId is null,
            CriadorUsuarioId = modelo.CriadorUsuarioId,
            Perguntas = perguntas
                .Select(p => new PerguntaDto { Id = p.Id, Texto = p.Texto, Peso = p.Peso, Ordem = p.Ordem })
                .ToList(),
            Faixas = faixas
                .Select(f => new FaixaDto
                {
                    Id = f.Id,
                    Titulo = f.Titulo,
                    Recomendacao = f.Recomendacao,
                    PontuacaoMin = f.PontuacaoMin,
                    PontuacaoMax = f.PontuacaoMax,
                    Cor = f.Cor,
                    Ordem = f.Ordem
                }).ToList()
        };
    }

    public static async Task<(bool Ok, string? Erro)> CriarTriagemAsync(int usuarioId, object payload)
    {
        var req = Converter<CriarTriagemInput>(payload);
        if (req is null) return (false, "Não foi possível ler os dados da triagem.");

        var erro = ValidarModelo(req);
        if (erro is not null) return (false, erro);

        var db = await ConexaoAsync();

        var modelo = new TriagemModeloLocal
        {
            Titulo = req.Titulo.Trim(),
            PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo!.Trim(),
            Descricao = req.Descricao?.Trim() ?? "",
            Icone = string.IsNullOrWhiteSpace(req.Icone) ? "📋" : req.Icone!.Trim(),
            CriadorUsuarioId = usuarioId,
            Ativa = true
        };

        await db.InsertAsync(modelo);
        await GravarPerguntasEFaixasAsync(db, modelo.Id, req);

        await db.InsertAsync(new HomePrefLocal
        {
            UsuarioId = usuarioId,
            TriagemModeloId = modelo.Id,
            Visivel = true,
            Ordem = 999
        });

        return (true, null);
    }

    public static async Task<(bool Ok, string? Erro)> AtualizarTriagemAsync(int usuarioId, int id, object payload)
    {
        var req = Converter<CriarTriagemInput>(payload);
        if (req is null) return (false, "Não foi possível ler os dados da triagem.");

        var erro = ValidarModelo(req);
        if (erro is not null) return (false, erro);

        var db = await ConexaoAsync();

        var modelo = await db.Table<TriagemModeloLocal>().Where(t => t.Id == id && t.Ativa).FirstOrDefaultAsync();
        if (modelo is null) return (false, "Triagem não encontrada.");
        if (modelo.CriadorUsuarioId != usuarioId) return (false, "Apenas o criador pode editar esta triagem.");

        modelo.Titulo = req.Titulo.Trim();
        modelo.PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo!.Trim();
        modelo.Descricao = req.Descricao?.Trim() ?? "";
        modelo.Icone = string.IsNullOrWhiteSpace(req.Icone) ? modelo.Icone : req.Icone!.Trim();
        await db.UpdateAsync(modelo);

        // As perguntas antigas são apagadas e regravadas (mesma estratégia da API).
        // O histórico já realizado guarda a pontuação e a classificação calculadas na
        // época, então continua legível mesmo com as perguntas trocadas.
        await db.ExecuteAsync("DELETE FROM perguntas WHERE TriagemModeloId = ?", id);
        await db.ExecuteAsync("DELETE FROM faixas WHERE TriagemModeloId = ?", id);
        await GravarPerguntasEFaixasAsync(db, id, req);

        return (true, null);
    }

    public static async Task<(bool Ok, string? Erro)> ExcluirTriagemAsync(int usuarioId, int id)
    {
        var db = await ConexaoAsync();

        var modelo = await db.Table<TriagemModeloLocal>().Where(t => t.Id == id).FirstOrDefaultAsync();
        if (modelo is null) return (false, "Triagem não encontrada.");
        if (modelo.CriadorUsuarioId != usuarioId) return (false, "Apenas o criador pode excluir esta triagem.");

        // Exclusão lógica: o histórico de aplicações continua apontando para o modelo.
        modelo.Ativa = false;
        await db.UpdateAsync(modelo);
        return (true, null);
    }

    private static async Task GravarPerguntasEFaixasAsync(SQLiteAsyncConnection db, int modeloId, CriarTriagemInput req)
    {
        var perguntas = req.Perguntas
            .Select((p, i) => new PerguntaLocal
            {
                TriagemModeloId = modeloId,
                Texto = p.Texto.Trim(),
                Peso = p.Peso,
                Ordem = i + 1
            }).ToList();

        var faixas = req.Faixas
            .OrderBy(f => f.PontuacaoMin)
            .Select((f, i) => new FaixaLocal
            {
                TriagemModeloId = modeloId,
                Titulo = f.Titulo.Trim(),
                Recomendacao = f.Recomendacao?.Trim() ?? "",
                PontuacaoMin = f.PontuacaoMin,
                PontuacaoMax = f.PontuacaoMax,
                Cor = string.IsNullOrWhiteSpace(f.Cor) ? CorPadrao(i) : f.Cor!,
                Ordem = i + 1
            }).ToList();

        await db.InsertAllAsync(perguntas);
        await db.InsertAllAsync(faixas);
    }

    // ---------------- Home ----------------

    public static async Task ConfigurarHomeAsync(int usuarioId, IEnumerable<(int TriagemModeloId, bool Visivel, int Ordem)> itens)
    {
        var db = await ConexaoAsync();

        var existentes = (await db.Table<HomePrefLocal>().Where(h => h.UsuarioId == usuarioId).ToListAsync())
            .ToDictionary(h => h.TriagemModeloId);

        foreach (var item in itens)
        {
            if (existentes.TryGetValue(item.TriagemModeloId, out var pref))
            {
                pref.Visivel = item.Visivel;
                pref.Ordem = item.Ordem;
                await db.UpdateAsync(pref);
            }
            else
            {
                await db.InsertAsync(new HomePrefLocal
                {
                    UsuarioId = usuarioId,
                    TriagemModeloId = item.TriagemModeloId,
                    Visivel = item.Visivel,
                    Ordem = item.Ordem
                });
            }
        }
    }

    // ---------------- Execução ----------------

    public static async Task<(ResultadoTriagem? Resultado, string? Erro)> ResponderAsync(int usuarioId, int triagemId, object payload)
    {
        var req = Converter<ResponderInput>(payload);
        if (req is null) return (null, "Não foi possível ler as respostas.");

        if (string.IsNullOrWhiteSpace(req.NomePaciente)) return (null, "Informe o nome da pessoa avaliada.");
        if (req.Idade is < 0 or > 130) return (null, "Idade inválida.");

        var db = await ConexaoAsync();

        var modelo = await db.Table<TriagemModeloLocal>().Where(t => t.Id == triagemId && t.Ativa).FirstOrDefaultAsync();
        if (modelo is null) return (null, "Triagem não encontrada.");

        var perguntas = await db.Table<PerguntaLocal>().Where(p => p.TriagemModeloId == triagemId).ToListAsync();
        var perguntasPorId = perguntas.ToDictionary(p => p.Id);

        var pontuacao = 0;
        foreach (var r in req.Respostas)
        {
            if (!perguntasPorId.TryGetValue(r.PerguntaId, out var pergunta))
                return (null, $"Pergunta {r.PerguntaId} não pertence a esta triagem.");
            if (r.Valor) pontuacao += pergunta.Peso;
        }

        var pontuacaoMaxima = perguntas.Sum(p => p.Peso);

        var faixas = (await db.Table<FaixaLocal>().Where(f => f.TriagemModeloId == triagemId).ToListAsync())
            .OrderBy(f => f.Ordem).ToList();

        var faixa = faixas.FirstOrDefault(f => pontuacao >= f.PontuacaoMin && pontuacao <= f.PontuacaoMax)
                    ?? faixas.LastOrDefault();

        var resultado = new ResultadoLocal
        {
            TriagemModeloId = modelo.Id,
            UsuarioId = usuarioId,
            NomePaciente = req.NomePaciente.Trim(),
            Idade = req.Idade,
            Sexo = req.Sexo?.Trim() ?? "",
            Pontuacao = pontuacao,
            PontuacaoMaxima = pontuacaoMaxima,
            Classificacao = faixa?.Titulo ?? "Sem classificação",
            Recomendacao = faixa?.Recomendacao ?? "",
            Cor = faixa?.Cor ?? "#10B981",
            Data = DateTime.UtcNow
        };

        await db.InsertAsync(resultado);
        await db.InsertAllAsync(req.Respostas.Select(r => new RespostaLocal
        {
            ResultadoId = resultado.Id,
            PerguntaId = r.PerguntaId,
            Valor = r.Valor
        }).ToList());

        return (new ResultadoTriagem
        {
            Id = resultado.Id,
            TriagemModeloId = modelo.Id,
            TituloTriagem = modelo.Titulo,
            NomePaciente = resultado.NomePaciente,
            Idade = resultado.Idade,
            Sexo = resultado.Sexo,
            Pontuacao = resultado.Pontuacao,
            PontuacaoMaxima = resultado.PontuacaoMaxima,
            Classificacao = resultado.Classificacao,
            Recomendacao = resultado.Recomendacao,
            Cor = resultado.Cor,
            Data = resultado.Data
        }, null);
    }

    // ---------------- Histórico ----------------

    private const int TamanhoPaginaMaximo = 200;

    public static async Task<List<HistoricoItem>> HistoricoAsync(int usuarioId, int? triagemId = null)
    {
        var db = await ConexaoAsync();

        var query = db.Table<ResultadoLocal>().Where(r => r.UsuarioId == usuarioId);
        if (triagemId is not null)
        {
            var filtro = triagemId.Value;
            query = query.Where(r => r.TriagemModeloId == filtro);
        }

        var resultados = await query
            .OrderByDescending(r => r.Data)
            .Take(TamanhoPaginaMaximo)
            .ToListAsync();

        var titulos = (await db.Table<TriagemModeloLocal>().ToListAsync())
            .ToDictionary(t => t.Id, t => t.Titulo);

        return resultados.Select(r => new HistoricoItem
        {
            Id = r.Id,
            TriagemModeloId = r.TriagemModeloId,
            TituloTriagem = titulos.TryGetValue(r.TriagemModeloId, out var titulo) ? titulo : "Triagem",
            Nome = r.NomePaciente,
            Idade = r.Idade,
            Sexo = r.Sexo,
            Pontuacao = r.Pontuacao,
            PontuacaoMaxima = r.PontuacaoMaxima,
            Resultado = r.Classificacao,
            Cor = r.Cor,
            Data = r.Data
        }).ToList();
    }

    // ---------------- Validação (espelha a da API) ----------------

    private static string? ValidarModelo(CriarTriagemInput req)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo)) return "Informe o título da triagem.";

        var perguntas = req.Perguntas;
        var faixas = req.Faixas;

        if (perguntas is null || perguntas.Count == 0) return "Adicione pelo menos uma pergunta.";
        if (perguntas.Count > 50) return "Máximo de 50 perguntas por triagem.";
        if (perguntas.Any(p => string.IsNullOrWhiteSpace(p.Texto))) return "Toda pergunta precisa de um texto.";
        if (perguntas.Any(p => p.Peso is < 1 or > 100)) return "O peso de cada pergunta deve estar entre 1 e 100.";
        if (faixas is null || faixas.Count < 2) return "Defina pelo menos duas faixas de resultado.";
        if (faixas.Any(f => string.IsNullOrWhiteSpace(f.Titulo))) return "Toda faixa de resultado precisa de um título.";
        if (faixas.Any(f => f.PontuacaoMin > f.PontuacaoMax)) return "Em cada faixa, a pontuação mínima deve ser menor ou igual à máxima.";

        var ordenadas = faixas.OrderBy(f => f.PontuacaoMin).ToList();
        for (var i = 1; i < ordenadas.Count; i++)
        {
            if (ordenadas[i].PontuacaoMin <= ordenadas[i - 1].PontuacaoMax)
                return "As faixas de resultado não podem se sobrepor.";
        }

        var pesoTotal = perguntas.Sum(p => p.Peso);
        if (ordenadas[0].PontuacaoMin > 0) return "A primeira faixa deve começar em 0.";
        if (ordenadas[^1].PontuacaoMax < pesoTotal)
            return $"A última faixa deve cobrir até a pontuação máxima ({pesoTotal}).";

        return null;
    }

    private static string CorPadrao(int indice) => indice switch
    {
        0 => "#10B981",
        1 => "#F59E0B",
        _ => "#EF4444",
    };

    // ---------------- Payloads ----------------
    // As telas montam os mesmos objetos anônimos que seriam enviados à API. Em vez de
    // duplicar essa montagem, o objeto é serializado e relido no formato local — assim
    // nenhuma tela precisa de um caminho de código diferente no modo local.

    private static T? Converter<T>(object payload) where T : class
    {
        if (payload is T tipado) return tipado;
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private sealed class PerguntaInputLocal
    {
        public string Texto { get; set; } = "";
        public int Peso { get; set; }
    }

    private sealed class FaixaInputLocal
    {
        public string Titulo { get; set; } = "";
        public string? Recomendacao { get; set; }
        public int PontuacaoMin { get; set; }
        public int PontuacaoMax { get; set; }
        public string? Cor { get; set; }
    }

    private sealed class CriarTriagemInput
    {
        public string Titulo { get; set; } = "";
        public string? PublicoAlvo { get; set; }
        public string? Descricao { get; set; }
        public string? Icone { get; set; }
        public List<PerguntaInputLocal> Perguntas { get; set; } = [];
        public List<FaixaInputLocal> Faixas { get; set; } = [];
    }

    private sealed class RespostaInputLocal
    {
        public int PerguntaId { get; set; }
        public bool Valor { get; set; }
    }

    private sealed class ResponderInput
    {
        public string NomePaciente { get; set; } = "";
        public int Idade { get; set; }
        public string? Sexo { get; set; }
        public List<RespostaInputLocal> Respostas { get; set; } = [];
    }

    // ---------------- Senha (PBKDF2, igual à API) ----------------

    private const int Iteracoes = 100_000;
    private const int TamanhoSalt = 16;
    private const int TamanhoChave = 32;

    private static string HashSenha(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var chave = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, TamanhoChave);
        return $"{Iteracoes}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(chave)}";
    }

    private static bool VerificarSenha(string senha, string hashArmazenado)
    {
        var partes = hashArmazenado.Split('.');
        if (partes.Length != 3) return false;
        if (!int.TryParse(partes[0], out var iteracoes)) return false;

        try
        {
            var salt = Convert.FromBase64String(partes[1]);
            var esperado = Convert.FromBase64String(partes[2]);
            var chave = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(chave, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // ---------------- Tabelas ----------------

    [Table("usuarios")]
    private sealed class UsuarioLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string Nome { get; set; } = "";
        [Indexed(Unique = true)] public string Email { get; set; } = "";
        public string SenhaHash { get; set; } = "";
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }

    [Table("triagem_modelos")]
    private sealed class TriagemModeloLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string PublicoAlvo { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string Icone { get; set; } = "🩺";

        /// <summary>Null = triagem padrão do sistema; senão, id do usuário criador.</summary>
        public int? CriadorUsuarioId { get; set; }
        public bool Ativa { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }

    [Table("perguntas")]
    private sealed class PerguntaLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public int TriagemModeloId { get; set; }
        public string Texto { get; set; } = "";
        public int Peso { get; set; } = 1;
        public int Ordem { get; set; }
    }

    [Table("faixas")]
    private sealed class FaixaLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public int TriagemModeloId { get; set; }
        public string Titulo { get; set; } = "";
        public string Recomendacao { get; set; } = "";
        public int PontuacaoMin { get; set; }
        public int PontuacaoMax { get; set; }
        public string Cor { get; set; } = "#10B981";
        public int Ordem { get; set; }
    }

    [Table("home_prefs")]
    private sealed class HomePrefLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public int UsuarioId { get; set; }
        public int TriagemModeloId { get; set; }
        public bool Visivel { get; set; } = true;
        public int Ordem { get; set; }
    }

    [Table("resultados")]
    private sealed class ResultadoLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public int UsuarioId { get; set; }
        public int TriagemModeloId { get; set; }
        public string NomePaciente { get; set; } = "";
        public int Idade { get; set; }
        public string Sexo { get; set; } = "";
        public int Pontuacao { get; set; }
        public int PontuacaoMaxima { get; set; }
        public string Classificacao { get; set; } = "";
        public string Recomendacao { get; set; } = "";
        public string Cor { get; set; } = "#10B981";
        public DateTime Data { get; set; } = DateTime.UtcNow;
    }

    [Table("respostas")]
    private sealed class RespostaLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public int ResultadoId { get; set; }
        public int PerguntaId { get; set; }
        public bool Valor { get; set; }
    }
}
