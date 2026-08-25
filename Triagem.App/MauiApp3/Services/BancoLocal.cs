using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Mail;
using MauiApp3.Models;
using SQLite;
using Triagem.Core.Domain;

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

            await LocalDataProtection.InicializarAsync();
            var conexao = new SQLiteAsyncConnection(CaminhoBanco,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

            await conexao.CreateTableAsync<UsuarioLocal>();
            await conexao.CreateTableAsync<TriagemModeloLocal>();
            await GarantirColunaImagemAsync(conexao);
            await conexao.CreateTableAsync<PerguntaLocal>();
            await conexao.CreateTableAsync<FaixaLocal>();
            await conexao.CreateTableAsync<HomePrefLocal>();
            await conexao.CreateTableAsync<ResultadoLocal>();
            await conexao.CreateTableAsync<RespostaLocal>();
            await GarantirColunasProtegidasAsync(conexao);
            await GarantirIndicesAsync(conexao);
            await MigrarDadosClinicosLegadosAsync(conexao);

            await SemearAsync(conexao);

            _conexao = conexao;
            return conexao;
        }
        finally
        {
            Inicializacao.Release();
        }
    }

    private static async Task GarantirColunaImagemAsync(SQLiteAsyncConnection conexao)
    {
        var existe = await conexao.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('triagem_modelos') WHERE name = 'Imagem'");
        if (existe == 0)
            await conexao.ExecuteAsync("ALTER TABLE triagem_modelos ADD COLUMN Imagem TEXT NULL");
    }

    private static async Task GarantirColunasProtegidasAsync(SQLiteAsyncConnection conexao)
    {
        var resultadoProtegido = await conexao.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('resultados') WHERE name = 'DadosProtegidos'");
        if (resultadoProtegido == 0)
            await conexao.ExecuteAsync("ALTER TABLE resultados ADD COLUMN DadosProtegidos TEXT NULL");

        var respostaProtegida = await conexao.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('respostas') WHERE name = 'ValorProtegido'");
        if (respostaProtegida == 0)
            await conexao.ExecuteAsync("ALTER TABLE respostas ADD COLUMN ValorProtegido TEXT NULL");
    }

    private static async Task GarantirIndicesAsync(SQLiteAsyncConnection conexao)
    {
        await conexao.ExecuteAsync("""
            DELETE FROM home_prefs
            WHERE Id NOT IN (
                SELECT MIN(Id) FROM home_prefs GROUP BY UsuarioId, TriagemModeloId
            )
            """);
        await conexao.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_home_prefs_usuario_triagem ON home_prefs (UsuarioId, TriagemModeloId)");
        await conexao.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_resultados_usuario_triagem_data ON resultados (UsuarioId, TriagemModeloId, Data DESC)");
    }

    private static async Task MigrarDadosClinicosLegadosAsync(SQLiteAsyncConnection conexao)
    {
        const int tamanhoLote = 200;
        while (true)
        {
            var resultados = await conexao.Table<ResultadoLocal>()
                .Where(r => r.DadosProtegidos == null)
                .Take(tamanhoLote)
                .ToListAsync();
            if (resultados.Count == 0) break;

            foreach (var r in resultados)
            {
                var dados = new ResultadoSensivelLocal
                {
                    NomePaciente = r.NomePaciente,
                    Idade = r.Idade,
                    Sexo = r.Sexo,
                    Pontuacao = r.Pontuacao,
                    PontuacaoMaxima = r.PontuacaoMaxima,
                    Classificacao = r.Classificacao,
                    Recomendacao = r.Recomendacao,
                    Cor = r.Cor
                };
                r.DadosProtegidos = LocalDataProtection.Proteger(JsonSerializer.Serialize(dados, JsonOptions));
                r.NomePaciente = "";
                r.Idade = 0;
                r.Sexo = "";
                r.Pontuacao = 0;
                r.PontuacaoMaxima = 0;
                r.Classificacao = "";
                r.Recomendacao = "";
                r.Cor = "#000000";
            }

            await conexao.RunInTransactionAsync(conn => conn.UpdateAll(resultados));
        }

        while (true)
        {
            var respostas = await conexao.Table<RespostaLocal>()
                .Where(r => r.ValorProtegido == null)
                .Take(tamanhoLote)
                .ToListAsync();
            if (respostas.Count == 0) break;

            foreach (var r in respostas)
            {
                r.ValorProtegido = LocalDataProtection.Proteger(r.Valor ? "1" : "0");
                r.Valor = false;
            }

            await conexao.RunInTransactionAsync(conn => conn.UpdateAll(respostas));
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
        if (senha.Length > 256)
            return (null, "A senha deve ter no máximo 256 caracteres.");
        if (nome.Trim().Length > 120)
            return (null, "O nome deve ter no máximo 120 caracteres.");
        if (email.Trim().Length > 180 || !MailAddress.TryCreate(email.Trim(), out _))
            return (null, "Informe um email válido.");

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

    public static async Task<Usuario?> ObterUsuarioAsync(int usuarioId)
    {
        var db = await ConexaoAsync();
        var usuario = await db.Table<UsuarioLocal>().Where(u => u.Id == usuarioId).FirstOrDefaultAsync();
        return usuario is null ? null : new Usuario { Id = usuario.Id, Nome = usuario.Nome, Email = usuario.Email };
    }

    public static async Task<Usuario> CriarUsuarioIndividualAsync()
    {
        var db = await ConexaoAsync();

        var usuario = new UsuarioLocal
        {
            Nome = "Você",
            Email = $"individual-{Guid.NewGuid():N}@local.triar",
            SenhaHash = HashSenha(Guid.NewGuid().ToString())
        };
        await db.InsertAsync(usuario);

        return new Usuario { Id = usuario.Id, Nome = usuario.Nome, Email = usuario.Email };
    }

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
            Imagem = t.Imagem,
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
            Imagem = modelo.Imagem,
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

    public static async Task<(bool Ok, string? Erro)> CriarTriagemAsync(int usuarioId, CriarTriagemPayload req)
    {
        var erro = ValidarModelo(req);
        if (erro is not null) return (false, erro);

        var db = await ConexaoAsync();

        var modelo = new TriagemModeloLocal
        {
            Titulo = req.Titulo.Trim(),
            PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo!.Trim(),
            Descricao = req.Descricao?.Trim() ?? "",
            Icone = string.IsNullOrWhiteSpace(req.Icone) ? "📋" : req.Icone!.Trim(),
            Imagem = NormalizarImagem(req.Imagem),
            CriadorUsuarioId = usuarioId,
            Ativa = true
        };

        await db.RunInTransactionAsync(conn =>
        {
            conn.Insert(modelo);
            GravarPerguntasEFaixas(conn, modelo.Id, req);
            conn.Insert(new HomePrefLocal
            {
                UsuarioId = usuarioId,
                TriagemModeloId = modelo.Id,
                Visivel = true,
                Ordem = 999
            });
        });

        return (true, null);
    }

    public static async Task<(bool Ok, string? Erro)> AtualizarTriagemAsync(int usuarioId, int id, CriarTriagemPayload req)
    {
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
        modelo.Imagem = NormalizarImagem(req.Imagem);

        // A atualização inteira é atômica: uma falha ao inserir perguntas/faixas novas
        // restaura também o modelo e as coleções antigas.
        await db.RunInTransactionAsync(conn =>
        {
            conn.Update(modelo);
            conn.Execute("DELETE FROM perguntas WHERE TriagemModeloId = ?", id);
            conn.Execute("DELETE FROM faixas WHERE TriagemModeloId = ?", id);
            GravarPerguntasEFaixas(conn, id, req);
        });

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

    private static void GravarPerguntasEFaixas(SQLiteConnection db, int modeloId, CriarTriagemPayload req)
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

        db.InsertAll(perguntas);
        db.InsertAll(faixas);
    }

    // ---------------- Home ----------------

    public static async Task ConfigurarHomeAsync(int usuarioId, IEnumerable<(int TriagemModeloId, bool Visivel, int Ordem)> itens)
    {
        var db = await ConexaoAsync();

        var existentes = (await db.Table<HomePrefLocal>().Where(h => h.UsuarioId == usuarioId).ToListAsync())
            .ToDictionary(h => h.TriagemModeloId);

        var alteracoes = itens.ToList();
        await db.RunInTransactionAsync(conn =>
        {
            foreach (var item in alteracoes)
            {
                if (existentes.TryGetValue(item.TriagemModeloId, out var pref))
                {
                    pref.Visivel = item.Visivel;
                    pref.Ordem = item.Ordem;
                    conn.Update(pref);
                }
                else
                {
                    conn.Insert(new HomePrefLocal
                    {
                        UsuarioId = usuarioId,
                        TriagemModeloId = item.TriagemModeloId,
                        Visivel = item.Visivel,
                        Ordem = item.Ordem
                    });
                }
            }
        });
    }

    // ---------------- Execução ----------------

    public static async Task<(ResultadoTriagem? Resultado, string? Erro)> ResponderAsync(int usuarioId, int triagemId, ResponderTriagemPayload req)
    {
        if (string.IsNullOrWhiteSpace(req.NomePaciente)) return (null, "Informe o nome da pessoa avaliada.");
        if (req.NomePaciente.Trim().Length > 150) return (null, "O nome deve ter no máximo 150 caracteres.");
        if (req.Idade is < 0 or > 130) return (null, "Idade inválida.");
        if ((req.Sexo?.Trim().Length ?? 0) > 30) return (null, "O sexo deve ter no máximo 30 caracteres.");

        var db = await ConexaoAsync();

        var modelo = await db.Table<TriagemModeloLocal>().Where(t => t.Id == triagemId && t.Ativa).FirstOrDefaultAsync();
        if (modelo is null) return (null, "Triagem não encontrada.");

        var perguntas = await db.Table<PerguntaLocal>().Where(p => p.TriagemModeloId == triagemId).ToListAsync();
        var perguntasPorId = perguntas.ToDictionary(p => p.Id);
        var respostasRecebidas = req.Respostas ?? [];

        var validacaoRespostas = TriagemRules.ValidarRespostas(
            perguntasPorId.Keys.ToList(), respostasRecebidas.Select(r => r.PerguntaId).ToList());
        var erroRespostas = MensagemErroRespostas(validacaoRespostas);
        if (erroRespostas is not null) return (null, erroRespostas);

        var pontuacao = 0;
        foreach (var r in respostasRecebidas)
        {
            if (!perguntasPorId.TryGetValue(r.PerguntaId, out var pergunta))
                return (null, $"Pergunta {r.PerguntaId} não pertence a esta triagem.");
            if (r.Valor) pontuacao += pergunta.Peso;
        }

        var pontuacaoMaxima = perguntas.Sum(p => p.Peso);
        if (pontuacao is < 0 || pontuacao > pontuacaoMaxima)
            return (null, "A pontuação calculada é inválida.");

        var faixas = (await db.Table<FaixaLocal>().Where(f => f.TriagemModeloId == triagemId).ToListAsync())
            .OrderBy(f => f.Ordem).ToList();

        var faixa = faixas.FirstOrDefault(f => pontuacao >= f.PontuacaoMin && pontuacao <= f.PontuacaoMax)
                    ?? faixas.LastOrDefault();

        var dadosSensiveis = new ResultadoSensivelLocal
        {
            NomePaciente = req.NomePaciente.Trim(),
            Idade = req.Idade,
            Sexo = req.Sexo?.Trim() ?? "",
            Pontuacao = pontuacao,
            PontuacaoMaxima = pontuacaoMaxima,
            Classificacao = faixa?.Titulo ?? "Sem classificação",
            Recomendacao = faixa?.Recomendacao ?? "",
            Cor = faixa?.Cor ?? "#10B981"
        };

        var resultado = new ResultadoLocal
        {
            TriagemModeloId = modelo.Id,
            UsuarioId = usuarioId,
            DadosProtegidos = LocalDataProtection.Proteger(JsonSerializer.Serialize(dadosSensiveis, JsonOptions)),
            Data = DateTime.UtcNow
        };

        var respostasLocais = respostasRecebidas.Select(r => new RespostaLocal
        {
            PerguntaId = r.PerguntaId,
            Valor = false,
            ValorProtegido = LocalDataProtection.Proteger(r.Valor ? "1" : "0")
        }).ToList();

        await db.RunInTransactionAsync(conn =>
        {
            conn.Insert(resultado);
            foreach (var resposta in respostasLocais)
                resposta.ResultadoId = resultado.Id;
            conn.InsertAll(respostasLocais);
        });

        return (new ResultadoTriagem
        {
            Id = resultado.Id,
            TriagemModeloId = modelo.Id,
            TituloTriagem = modelo.Titulo,
            NomePaciente = dadosSensiveis.NomePaciente,
            Idade = dadosSensiveis.Idade,
            Sexo = dadosSensiveis.Sexo,
            Pontuacao = dadosSensiveis.Pontuacao,
            PontuacaoMaxima = dadosSensiveis.PontuacaoMaxima,
            Classificacao = dadosSensiveis.Classificacao,
            Recomendacao = dadosSensiveis.Recomendacao,
            Cor = dadosSensiveis.Cor,
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

        return resultados.Select(r =>
        {
            var dados = ObterDadosSensiveis(r);
            return new HistoricoItem
            {
                Id = r.Id,
                TriagemModeloId = r.TriagemModeloId,
                TituloTriagem = titulos.TryGetValue(r.TriagemModeloId, out var titulo) ? titulo : "Triagem",
                Nome = dados.NomePaciente,
                Idade = dados.Idade,
                Sexo = dados.Sexo,
                Pontuacao = dados.Pontuacao,
                PontuacaoMaxima = dados.PontuacaoMaxima,
                Resultado = dados.Classificacao,
                Cor = dados.Cor,
                Data = r.Data
            };
        }).ToList();
    }

    private static ResultadoSensivelLocal ObterDadosSensiveis(ResultadoLocal resultado)
    {
        if (!string.IsNullOrWhiteSpace(resultado.DadosProtegidos))
        {
            var json = LocalDataProtection.Desproteger(resultado.DadosProtegidos);
            var protegido = JsonSerializer.Deserialize<ResultadoSensivelLocal>(json, JsonOptions);
            if (protegido is not null) return protegido;
        }

        return new ResultadoSensivelLocal
        {
            NomePaciente = resultado.NomePaciente,
            Idade = resultado.Idade,
            Sexo = resultado.Sexo,
            Pontuacao = resultado.Pontuacao,
            PontuacaoMaxima = resultado.PontuacaoMaxima,
            Classificacao = resultado.Classificacao,
            Recomendacao = resultado.Recomendacao,
            Cor = resultado.Cor
        };
    }

    // ---------------- Validação ----------------
    // A validação de fato (título/perguntas/faixas/imagem) vive em Triagem.Core.TriagemRules,
    // compartilhada com a API (TriagemService) — aqui só convertemos o payload do app
    // para os tipos de entrada do Core, garantindo que os dois modos nunca divirjam.

    private static string? ValidarModelo(CriarTriagemPayload req)
    {
        var erroImagem = TriagemRules.ValidarImagemBase64(req.Imagem);
        if (erroImagem is not null) return erroImagem;

        return TriagemRules.ValidarModelo(
            req.Titulo,
            req.Perguntas?.Select(p => new PerguntaEntrada(p.Texto, p.Peso)).ToList(),
            req.Faixas?.Select(f => new FaixaEntrada(f.Titulo, f.Recomendacao, f.PontuacaoMin, f.PontuacaoMax, f.Cor)).ToList());
    }

    private static string? NormalizarImagem(string? imagem) => TriagemRules.NormalizarImagem(imagem);

    private static string? MensagemErroRespostas(RespostasValidation validacao) => TriagemRules.MensagemErroRespostas(validacao);

    private static string CorPadrao(int indice) => TriagemRules.CorPadrao(indice);

    // ---------------- Envelope clínico local ----------------
    private sealed class ResultadoSensivelLocal
    {
        public string NomePaciente { get; set; } = "";
        public int Idade { get; set; }
        public string Sexo { get; set; } = "";
        public int Pontuacao { get; set; }
        public int PontuacaoMaxima { get; set; }
        public string Classificacao { get; set; } = "";
        public string Recomendacao { get; set; } = "";
        public string Cor { get; set; } = "#10B981";
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
        public string? Imagem { get; set; }

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
        [Indexed("IX_home_prefs_usuario_triagem", 1, Unique = true)] public int UsuarioId { get; set; }
        [Indexed("IX_home_prefs_usuario_triagem", 2, Unique = true)] public int TriagemModeloId { get; set; }
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
        public string? DadosProtegidos { get; set; }
        public DateTime Data { get; set; } = DateTime.UtcNow;
    }

    [Table("respostas")]
    private sealed class RespostaLocal
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public int ResultadoId { get; set; }
        public int PerguntaId { get; set; }
        public bool Valor { get; set; }
        public string? ValorProtegido { get; set; }
    }
}
