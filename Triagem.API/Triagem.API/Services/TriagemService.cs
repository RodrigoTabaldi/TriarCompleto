using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Triagem.API.Data;
using Triagem.API.Dtos;
using Triagem.API.Models;
using Triagem.Core.Domain;

namespace Triagem.API.Services;

public partial class TriagemService(
    TriagemDbContext db,
    CacheService cache,
    FieldEncryptionService encryptor,
    ILogger<TriagemService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UsuarioId} criou a triagem {TriagemId} ({Titulo})")]
    private static partial void LogTriagemCriada(ILogger logger, int usuarioId, int triagemId, string titulo);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Usuário {UsuarioId} acessou o histórico (triagemModeloId={TriagemModeloId}, página={Pagina}, {Quantidade} registro(s))")]
    private static partial void LogHistoricoAcessado(ILogger logger, int usuarioId, int? triagemModeloId, int pagina, int quantidade);

    private Task InvalidateCacheAsync() => cache.BumpVersionAsync();

    // ---------------- Modelos ----------------

    public async Task<List<TriagemModeloResumo>> ListarParaUsuarioAsync(int usuarioId, CancellationToken ct = default)
    {
        var versao = await cache.GetVersionAsync();
        var chave = $"triar:triagens:v{versao}:usuario:{usuarioId}";
        var resultado = await cache.GetOrCreateAsync(chave, CacheTtl, async () =>
        {
            var modelos = await db.TriagemModelos
                .AsNoTracking()
                .Where(t => t.Ativa && (t.CriadorUsuarioId == null || t.CriadorUsuarioId == usuarioId))
                .OrderBy(t => t.CriadorUsuarioId == null ? 0 : 1).ThenBy(t => t.Id)
                .Select(t => new
                {
                    t.Id, t.Titulo, t.PublicoAlvo, t.Descricao, t.Icone, t.Imagem,
                    t.CriadorUsuarioId,
                    TotalPerguntas = t.Perguntas.Count
                })
                .ToListAsync(ct);

            var prefs = await db.UsuarioTriagensHome
                .AsNoTracking()
                .Where(h => h.UsuarioId == usuarioId)
                .ToDictionaryAsync(h => h.TriagemModeloId, ct);

            return modelos.Select(t => new TriagemModeloResumo(
                t.Id, t.Titulo, t.PublicoAlvo, t.Descricao, t.Icone, t.Imagem,
                Padrao: t.CriadorUsuarioId == null,
                MinhaAutoria: t.CriadorUsuarioId == usuarioId,
                VisivelNaHome: !prefs.TryGetValue(t.Id, out var p) || p.Visivel,
                TotalPerguntas: t.TotalPerguntas)).ToList();
        });
        return resultado ?? [];
    }

    /// <summary>
    /// Detalhe de uma triagem (perguntas + faixas). Restrito às triagens padrão do
    /// sistema (CriadorUsuarioId nulo) ou criadas pelo próprio usuarioId — mesma regra
    /// de visibilidade de ListarParaUsuarioAsync, para não vazar triagens privadas de
    /// outros usuários por enumeração de id (IDOR).
    /// </summary>
    public async Task<TriagemModeloDetalhe?> ObterDetalheAsync(int usuarioId, int id, CancellationToken ct = default)
    {
        var versao = await cache.GetVersionAsync();
        var chave = $"triar:triagens:v{versao}:detalhe:{id}:usuario:{usuarioId}";
        return await cache.GetOrCreateAsync(chave, CacheTtl, async () =>
        {
            var t = await db.TriagemModelos
                .AsNoTracking()
                .Include(x => x.Perguntas)
                .Include(x => x.Faixas)
                .FirstOrDefaultAsync(x => x.Id == id && x.Ativa &&
                    (x.CriadorUsuarioId == null || x.CriadorUsuarioId == usuarioId), ct);

            if (t is null) return null;

            return new TriagemModeloDetalhe(
                t.Id, t.Titulo, t.PublicoAlvo, t.Descricao, t.Icone, t.Imagem,
                t.CriadorUsuarioId == null, t.CriadorUsuarioId,
                t.Perguntas.OrderBy(p => p.Ordem)
                    .Select(p => new PerguntaDto(p.Id, p.Texto, p.Peso, p.Ordem)).ToList(),
                t.Faixas.OrderBy(f => f.Ordem)
                    .Select(f => new FaixaDto(f.Id, f.Titulo, f.Recomendacao, f.PontuacaoMin, f.PontuacaoMax, f.Cor, f.Ordem)).ToList());
        });
    }

    public async Task<(TriagemModeloDetalhe? Detalhe, string? Erro)> CriarAsync(int usuarioId, CriarTriagemRequest req, CancellationToken ct = default)
    {
        var erro = ValidarModelo(req.Titulo, req.Perguntas, req.Faixas);
        if (erro is not null) return (null, erro);
        erro = ValidarImagem(req.Imagem);
        if (erro is not null) return (null, erro);

        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId, ct))
            return (null, "Usuário não encontrado.");

        var modelo = new TriagemModelo
        {
            Titulo = req.Titulo.Trim(),
            PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo.Trim(),
            Descricao = req.Descricao?.Trim() ?? "",
            Icone = string.IsNullOrWhiteSpace(req.Icone) ? "📋" : req.Icone.Trim(),
            Imagem = NormalizarImagem(req.Imagem),
            CriadorUsuarioId = usuarioId,
            Ativa = true,
            Perguntas = MapearPerguntas(req.Perguntas),
            Faixas = MapearFaixas(req.Faixas)
        };

        // Uma transação garante que o modelo e a preferência de home sejam gravados
        // juntos — sem risco de uma triagem "órfã" na home se a segunda gravação falhar.
        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            db.TriagemModelos.Add(modelo);
            await db.SaveChangesAsync(ct);

            db.UsuarioTriagensHome.Add(new UsuarioTriagemHome
            {
                UsuarioId = usuarioId,
                TriagemModeloId = modelo.Id,
                Visivel = true,
                Ordem = 999
            });
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
        });

        await InvalidateCacheAsync();
        LogTriagemCriada(logger, usuarioId, modelo.Id, modelo.Titulo);

        return (await ObterDetalheAsync(usuarioId, modelo.Id, ct), null);
    }

    public async Task<(bool Ok, string? Erro)> AtualizarAsync(int usuarioId, int id, CriarTriagemRequest req, CancellationToken ct = default)
    {
        var erro = ValidarModelo(req.Titulo, req.Perguntas, req.Faixas);
        if (erro is not null) return (false, erro);
        erro = ValidarImagem(req.Imagem);
        if (erro is not null) return (false, erro);

        var modelo = await db.TriagemModelos
            .Include(t => t.Perguntas)
            .Include(t => t.Faixas)
            .FirstOrDefaultAsync(t => t.Id == id && t.Ativa, ct);

        if (modelo is null) return (false, "Triagem não encontrada.");
        if (modelo.CriadorUsuarioId != usuarioId)
            return (false, "Apenas o criador pode editar esta triagem.");

        modelo.Titulo = req.Titulo.Trim();
        modelo.PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo.Trim();
        modelo.Descricao = req.Descricao?.Trim() ?? "";
        modelo.Icone = string.IsNullOrWhiteSpace(req.Icone) ? modelo.Icone : req.Icone.Trim();
        modelo.Imagem = NormalizarImagem(req.Imagem);

        db.Perguntas.RemoveRange(modelo.Perguntas);
        db.FaixasResultado.RemoveRange(modelo.Faixas);
        modelo.Perguntas = MapearPerguntas(req.Perguntas);
        modelo.Faixas = MapearFaixas(req.Faixas);

        await db.SaveChangesAsync(ct);
        await InvalidateCacheAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Erro)> DesativarAsync(int usuarioId, int id, CancellationToken ct = default)
    {
        var modelo = await db.TriagemModelos.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (modelo is null) return (false, "Triagem não encontrada.");
        if (modelo.CriadorUsuarioId != usuarioId)
            return (false, "Apenas o criador pode excluir esta triagem.");

        modelo.Ativa = false;
        await db.SaveChangesAsync(ct);
        await InvalidateCacheAsync();
        return (true, null);
    }

    // ---------------- Home ----------------

    public async Task<string?> ConfigurarHomeAsync(int usuarioId, ConfigurarHomeRequest req, CancellationToken ct = default)
    {
        if (req.Itens is null || req.Itens.Count > 100)
            return "A configuração da home deve conter no máximo 100 itens.";
        if (req.Itens.Select(i => i.TriagemModeloId).Distinct().Count() != req.Itens.Count)
            return "Cada triagem deve aparecer uma única vez na configuração da home.";

        var idsPermitidos = await db.TriagemModelos.AsNoTracking()
            .Where(t => t.Ativa && (t.CriadorUsuarioId == null || t.CriadorUsuarioId == usuarioId))
            .Select(t => t.Id)
            .ToHashSetAsync(ct);
        if (req.Itens.Any(i => !idsPermitidos.Contains(i.TriagemModeloId)))
            return "A configuração contém uma triagem indisponível para este usuário.";

        var existentes = await db.UsuarioTriagensHome
            .Where(h => h.UsuarioId == usuarioId)
            .ToDictionaryAsync(h => h.TriagemModeloId, ct);

        foreach (var item in req.Itens)
        {
            if (existentes.TryGetValue(item.TriagemModeloId, out var h))
            {
                h.Visivel = item.Visivel;
                h.Ordem = item.Ordem;
            }
            else
            {
                db.UsuarioTriagensHome.Add(new UsuarioTriagemHome
                {
                    UsuarioId = usuarioId,
                    TriagemModeloId = item.TriagemModeloId,
                    Visivel = item.Visivel,
                    Ordem = item.Ordem
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await InvalidateCacheAsync();
        return null;
    }

    // ---------------- Execução ----------------

    public async Task<(ResultadoResponse? Resultado, string? Erro)> ResponderAsync(int usuarioId, int triagemModeloId, ResponderTriagemRequest req, CancellationToken ct = default)
    {
        var modelo = await db.TriagemModelos
            .Include(t => t.Perguntas)
            .Include(t => t.Faixas)
            .FirstOrDefaultAsync(t => t.Id == triagemModeloId && t.Ativa &&
                (t.CriadorUsuarioId == null || t.CriadorUsuarioId == usuarioId), ct);

        if (modelo is null) return (null, "Triagem não encontrada.");
        if (string.IsNullOrWhiteSpace(req.NomePaciente)) return (null, "Informe o nome da pessoa avaliada.");
        if (req.NomePaciente.Trim().Length > 150) return (null, "O nome deve ter no máximo 150 caracteres.");
        if (req.Idade is < 0 or > 130) return (null, "Idade inválida.");
        if ((req.Sexo?.Trim().Length ?? 0) > 30) return (null, "O sexo deve ter no máximo 30 caracteres.");
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId, ct)) return (null, "Usuário não encontrado.");

        var perguntasPorId = modelo.Perguntas.ToDictionary(p => p.Id);
        var respostasRecebidas = req.Respostas ?? [];
        var validacaoRespostas = TriagemRules.ValidarRespostas(
            perguntasPorId.Keys.ToList(), respostasRecebidas.Select(r => r.PerguntaId).ToList());
        var erroRespostas = MensagemErroRespostas(validacaoRespostas);
        if (erroRespostas is not null) return (null, erroRespostas);

        var pontuacao = 0;
        var respostas = new List<RespostaDada>();

        foreach (var r in respostasRecebidas)
        {
            if (!perguntasPorId.TryGetValue(r.PerguntaId, out var pergunta))
                return (null, $"Pergunta {r.PerguntaId} não pertence a esta triagem.");

            if (r.Valor) pontuacao += pergunta.Peso;
            respostas.Add(new RespostaDada
            {
                PerguntaId = r.PerguntaId,
                Valor = false,
                ValorProtegido = encryptor.Encrypt(r.Valor ? "1" : "0")
            });
        }

        var pontuacaoMaxima = modelo.Perguntas.Sum(p => p.Peso);
        if (pontuacao is < 0 || pontuacao > pontuacaoMaxima)
            return (null, "A pontuação calculada é inválida.");

        var faixa = modelo.Faixas
            .OrderBy(f => f.Ordem)
            .FirstOrDefault(f => pontuacao >= f.PontuacaoMin && pontuacao <= f.PontuacaoMax)
            ?? modelo.Faixas.OrderBy(f => f.Ordem).LastOrDefault();

        var dadosSensiveis = new ResultadoSensivel
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

        var resultado = new TriagemResultado
        {
            TriagemModeloId = modelo.Id,
            UsuarioId = usuarioId,
            NomePaciente = "",
            DadosProtegidos = encryptor.Encrypt(JsonSerializer.Serialize(dadosSensiveis)),
            Respostas = respostas
        };

        db.TriagemResultados.Add(resultado);
        await db.SaveChangesAsync(ct);

        return (new ResultadoResponse(
            resultado.Id, modelo.Id, modelo.Titulo,
            dadosSensiveis.NomePaciente, dadosSensiveis.Idade, dadosSensiveis.Sexo,
            dadosSensiveis.Pontuacao, dadosSensiveis.PontuacaoMaxima,
            dadosSensiveis.Classificacao, dadosSensiveis.Recomendacao, dadosSensiveis.Cor, resultado.Data), null);
    }

    private const int TamanhoPaginaPadrao = 100;
    private const int TamanhoPaginaMaximo = 200;

    /// <summary>
    /// Histórico do usuário, paginado (mais recentes primeiro) para evitar que a
    /// consulta e a resposta cresçam sem limite conforme o histórico aumenta.
    /// </summary>
    public async Task<List<HistoricoItem>> HistoricoAsync(
        int usuarioId, int? triagemModeloId = null, int pagina = 1, int tamanhoPagina = TamanhoPaginaPadrao,
        CancellationToken ct = default)
    {
        pagina = Math.Max(pagina, 1);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, TamanhoPaginaMaximo);

        var query = db.TriagemResultados
            .AsNoTracking()
            .Include(r => r.TriagemModelo)
            .Where(r => r.UsuarioId == usuarioId);

        if (triagemModeloId is not null)
            query = query.Where(r => r.TriagemModeloId == triagemModeloId);

        var registros = await query
            .OrderByDescending(r => r.Data)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(r => new
            {
                r.Id, r.TriagemModeloId,
                TituloTriagem = r.TriagemModelo!.Titulo,
                r.NomePaciente, r.Idade, r.Sexo, r.Pontuacao, r.PontuacaoMaxima,
                r.Classificacao, r.Recomendacao, r.Cor, r.Data, r.DadosProtegidos
            })
            .ToListAsync(ct);

        var resultado = registros.Select(r =>
        {
            var dados = ObterDadosSensiveis(
                r.DadosProtegidos, r.NomePaciente, r.Idade, r.Sexo,
                r.Pontuacao, r.PontuacaoMaxima, r.Classificacao, r.Recomendacao, r.Cor);
            return new HistoricoItem(
                r.Id, r.TriagemModeloId, r.TituloTriagem,
                dados.NomePaciente, dados.Idade, dados.Sexo,
                dados.Pontuacao, dados.PontuacaoMaxima, dados.Classificacao,
                dados.Cor, r.Data);
        }).ToList();

        // Trilha de auditoria mínima: quem acessou dados de pacientes (nome + respostas
        // de saúde), quando e quantos registros — sem logar o conteúdo sensível em si.
        LogHistoricoAcessado(logger, usuarioId, triagemModeloId, pagina, resultado.Count);

        return resultado;
    }

    private ResultadoSensivel ObterDadosSensiveis(
        string? protegido, string nome, int idade, string sexo,
        int pontuacao, int pontuacaoMaxima, string classificacao, string recomendacao, string cor)
    {
        if (!string.IsNullOrWhiteSpace(protegido))
        {
            var dados = JsonSerializer.Deserialize<ResultadoSensivel>(encryptor.Decrypt(protegido));
            if (dados is not null) return dados;
        }

        return new ResultadoSensivel
        {
            NomePaciente = nome,
            Idade = idade,
            Sexo = sexo,
            Pontuacao = pontuacao,
            PontuacaoMaxima = pontuacaoMaxima,
            Classificacao = classificacao,
            Recomendacao = recomendacao,
            Cor = cor
        };
    }

    private sealed class ResultadoSensivel
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

    // ---------------- Mapeamento (compartilhado por Criar/Atualizar) ----------------

    private static List<Pergunta> MapearPerguntas(List<PerguntaInput> perguntas) =>
        perguntas
            .Select((p, i) => new Pergunta { Texto = p.Texto.Trim(), Peso = p.Peso, Ordem = i + 1 })
            .ToList();

    private static List<FaixaResultado> MapearFaixas(List<FaixaInput> faixas) =>
        faixas
            .OrderBy(f => f.PontuacaoMin)
            .Select((f, i) => new FaixaResultado
            {
                Titulo = f.Titulo.Trim(),
                Recomendacao = f.Recomendacao?.Trim() ?? "",
                PontuacaoMin = f.PontuacaoMin,
                PontuacaoMax = f.PontuacaoMax,
                Cor = string.IsNullOrWhiteSpace(f.Cor) ? TriagemRules.CorPadrao(i) : f.Cor!,
                Ordem = i + 1
            }).ToList();

    // ---------------- Validação ----------------
    // A validação de fato (título/perguntas/faixas/imagem) vive em Triagem.Core.TriagemRules,
    // compartilhada com o modo offline do app (BancoLocal) — aqui só convertemos os
    // DTOs da API para os tipos de entrada do Core.

    private static string? ValidarModelo(string titulo, List<PerguntaInput> perguntas, List<FaixaInput> faixas) =>
        TriagemRules.ValidarModelo(
            titulo,
            perguntas?.Select(p => new PerguntaEntrada(p.Texto, p.Peso)).ToList(),
            faixas?.Select(f => new FaixaEntrada(f.Titulo, f.Recomendacao, f.PontuacaoMin, f.PontuacaoMax, f.Cor)).ToList());

    private static string? NormalizarImagem(string? imagem) => TriagemRules.NormalizarImagem(imagem);

    private static string? ValidarImagem(string? imagem) => TriagemRules.ValidarImagemBase64(imagem);

    private static string? MensagemErroRespostas(RespostasValidation validacao) => TriagemRules.MensagemErroRespostas(validacao);
}
