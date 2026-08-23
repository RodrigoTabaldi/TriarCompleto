using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Triagem.API.Data;
using Triagem.API.Dtos;
using Triagem.API.Models;

namespace Triagem.API.Services;

public partial class TriagemService(TriagemDbContext db, CacheService cache, ILogger<TriagemService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int MaxItensHome = 200;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex CorHexRegex();
    private static readonly Regex CorHexValida = CorHexRegex();

    private Task InvalidateCacheAsync() => cache.BumpVersionAsync();

    /// <summary>
    /// Regra única de visibilidade de uma triagem para um usuário: padrão do sistema
    /// (CriadorUsuarioId nulo) ou criada por ele mesmo. Usada em toda leitura/escrita
    /// que recebe um id de triagem vindo do cliente, para que nenhum caminho novo possa
    /// esquecer a checagem e reabrir IDOR (ver ObterDetalheAsync, ResponderAsync).
    /// </summary>
    private static Expression<Func<TriagemModelo, bool>> VisivelPara(int usuarioId) =>
        t => t.CriadorUsuarioId == null || t.CriadorUsuarioId == usuarioId;

    // ---------------- Modelos ----------------

    public async Task<List<TriagemModeloResumo>> ListarParaUsuarioAsync(int usuarioId)
    {
        var versao = await cache.GetVersionAsync();
        var chave = $"triar:triagens:v{versao}:usuario:{usuarioId}";
        var resultado = await cache.GetOrCreateAsync(chave, CacheTtl, async () =>
        {
            var modelos = await db.TriagemModelos
                .AsNoTracking()
                .Where(t => t.Ativa && (t.CriadorUsuarioId == null || t.CriadorUsuarioId == usuarioId))
                .Include(t => t.Perguntas)
                .OrderBy(t => t.CriadorUsuarioId == null ? 0 : 1).ThenBy(t => t.Id)
                .ToListAsync();

            var prefs = await db.UsuarioTriagensHome
                .AsNoTracking()
                .Where(h => h.UsuarioId == usuarioId)
                .ToDictionaryAsync(h => h.TriagemModeloId);

            return modelos.Select(t => new TriagemModeloResumo(
                t.Id, t.Titulo, t.PublicoAlvo, t.Descricao, t.Icone,
                Padrao: t.CriadorUsuarioId == null,
                MinhaAutoria: t.CriadorUsuarioId == usuarioId,
                VisivelNaHome: !prefs.TryGetValue(t.Id, out var p) || p.Visivel,
                TotalPerguntas: t.Perguntas.Count)).ToList();
        });
        return resultado ?? [];
    }

    /// <summary>
    /// Detalhe de uma triagem (perguntas + faixas). Restrito às triagens padrão do
    /// sistema (CriadorUsuarioId nulo) ou criadas pelo próprio usuarioId — mesma regra
    /// de visibilidade de ListarParaUsuarioAsync, para não vazar triagens privadas de
    /// outros usuários por enumeração de id (IDOR).
    /// </summary>
    public async Task<TriagemModeloDetalhe?> ObterDetalheAsync(int usuarioId, int id)
    {
        var versao = await cache.GetVersionAsync();

        // Para as triagens padrão do sistema (CriadorUsuarioId nulo), o conteúdo do
        // detalhe é idêntico para qualquer usuário — Padrao é sempre true e
        // CriadorUsuarioId sempre null, então não há nada usuário-dependente na
        // resposta. Sem esta checagem, cada uma das poucas triagens padrão gerava uma
        // entrada de cache por usuário que já a acessou (6 triagens × N usuários),
        // acelerando a evicção de entradas úteis num Redis com memória limitada.
        // Para triagens privadas a partição por usuário é mantida (só o dono acessa a
        // própria, então não há multiplicação real ali) — a checagem de acesso
        // continua sempre aplicada dentro da consulta, então um resultado incorreto
        // aqui no máximo perde o atalho de cache compartilhado, nunca abre acesso.
        var idsPadrao = await cache.GetOrCreateAsync(
            $"triar:triagens:v{versao}:ids-padrao",
            CacheTtl,
            async () => await db.TriagemModelos
                .AsNoTracking()
                .Where(t => t.Ativa && t.CriadorUsuarioId == null)
                .Select(t => t.Id)
                .ToListAsync());

        var chave = idsPadrao is not null && idsPadrao.Contains(id)
            ? $"triar:triagens:v{versao}:detalhe:{id}"
            : $"triar:triagens:v{versao}:detalhe:{id}:usuario:{usuarioId}";

        return await cache.GetOrCreateAsync(chave, CacheTtl, async () =>
        {
            var t = await db.TriagemModelos
                .AsNoTracking()
                .Include(x => x.Perguntas)
                .Include(x => x.Faixas)
                .Where(x => x.Id == id && x.Ativa)
                .Where(VisivelPara(usuarioId))
                .FirstOrDefaultAsync();

            if (t is null) return null;

            return new TriagemModeloDetalhe(
                t.Id, t.Titulo, t.PublicoAlvo, t.Descricao, t.Icone,
                t.CriadorUsuarioId == null, t.CriadorUsuarioId,
                t.Perguntas.OrderBy(p => p.Ordem)
                    .Select(p => new PerguntaDto(p.Id, p.Texto, p.Peso, p.Ordem)).ToList(),
                t.Faixas.OrderBy(f => f.Ordem)
                    .Select(f => new FaixaDto(f.Id, f.Titulo, f.Recomendacao, f.PontuacaoMin, f.PontuacaoMax, f.Cor, f.Ordem)).ToList());
        });
    }

    public async Task<(TriagemModeloDetalhe? Detalhe, string? Erro)> CriarAsync(int usuarioId, CriarTriagemRequest req)
    {
        var erro = ValidarModelo(req.Titulo, req.Perguntas, req.Faixas);
        if (erro is not null) return (null, erro);

        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId))
            return (null, "Usuário não encontrado.");

        var modelo = new TriagemModelo
        {
            Titulo = req.Titulo.Trim(),
            PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo.Trim(),
            Descricao = req.Descricao?.Trim() ?? "",
            Icone = string.IsNullOrWhiteSpace(req.Icone) ? "📋" : req.Icone.Trim(),
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
            await using var tx = await db.Database.BeginTransactionAsync();

            db.TriagemModelos.Add(modelo);
            await db.SaveChangesAsync();

            db.UsuarioTriagensHome.Add(new UsuarioTriagemHome
            {
                UsuarioId = usuarioId,
                TriagemModeloId = modelo.Id,
                Visivel = true,
                Ordem = 999
            });
            await db.SaveChangesAsync();

            await tx.CommitAsync();
        });

        await InvalidateCacheAsync();
        logger.LogInformation("Usuário {UsuarioId} criou a triagem {TriagemId} ({Titulo})",
            usuarioId, modelo.Id, modelo.Titulo);

        return (await ObterDetalheAsync(usuarioId, modelo.Id), null);
    }

    public async Task<(bool Ok, string? Erro)> AtualizarAsync(int usuarioId, int id, CriarTriagemRequest req)
    {
        var erro = ValidarModelo(req.Titulo, req.Perguntas, req.Faixas);
        if (erro is not null) return (false, erro);

        var modelo = await db.TriagemModelos
            .Include(t => t.Perguntas)
            .Include(t => t.Faixas)
            .FirstOrDefaultAsync(t => t.Id == id && t.Ativa);

        if (modelo is null) return (false, "Triagem não encontrada.");
        if (modelo.CriadorUsuarioId != usuarioId)
            return (false, "Apenas o criador pode editar esta triagem.");

        modelo.Titulo = req.Titulo.Trim();
        modelo.PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo.Trim();
        modelo.Descricao = req.Descricao?.Trim() ?? "";
        modelo.Icone = string.IsNullOrWhiteSpace(req.Icone) ? modelo.Icone : req.Icone.Trim();

        db.Perguntas.RemoveRange(modelo.Perguntas);
        db.FaixasResultado.RemoveRange(modelo.Faixas);
        modelo.Perguntas = MapearPerguntas(req.Perguntas);
        modelo.Faixas = MapearFaixas(req.Faixas);

        await db.SaveChangesAsync();
        await InvalidateCacheAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Erro)> DesativarAsync(int usuarioId, int id)
    {
        var modelo = await db.TriagemModelos.FirstOrDefaultAsync(t => t.Id == id);
        if (modelo is null) return (false, "Triagem não encontrada.");
        if (modelo.CriadorUsuarioId != usuarioId)
            return (false, "Apenas o criador pode excluir esta triagem.");

        modelo.Ativa = false;
        await db.SaveChangesAsync();
        await InvalidateCacheAsync();
        return (true, null);
    }

    // ---------------- Home ----------------

    public async Task<(bool Ok, string? Erro)> ConfigurarHomeAsync(int usuarioId, ConfigurarHomeRequest req)
    {
        if (req.Itens.Count > MaxItensHome)
            return (false, $"Máximo de {MaxItensHome} itens por requisição.");

        // Sem isto, o cliente podia gravar preferência de home para QUALQUER
        // TriagemModeloId — inclusive de triagem privada de outro usuário (linha órfã,
        // sem vazamento de conteúdo pois a leitura já filtra) ou inexistente
        // (DbUpdateException por violação de FK, virando HTTP 500 não tratado).
        var idsVisiveis = (await db.TriagemModelos
            .Where(VisivelPara(usuarioId))
            .Select(t => t.Id)
            .ToListAsync())
            .ToHashSet();

        var itensValidos = req.Itens.Where(i => idsVisiveis.Contains(i.TriagemModeloId)).ToList();

        var existentes = await db.UsuarioTriagensHome
            .Where(h => h.UsuarioId == usuarioId)
            .ToDictionaryAsync(h => h.TriagemModeloId);

        foreach (var item in itensValidos)
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

        await db.SaveChangesAsync();
        await InvalidateCacheAsync();
        return (true, null);
    }

    // ---------------- Execução ----------------

    public async Task<(ResultadoResponse? Resultado, string? Erro)> ResponderAsync(int usuarioId, int triagemModeloId, ResponderTriagemRequest req)
    {
        // Mesma regra de visibilidade de ObterDetalheAsync: sem ela, qualquer usuário
        // autenticado poderia responder (e ler de volta título/classificação/recomendação
        // de) uma triagem privada de outro usuário só sabendo o id — IDOR.
        var modelo = await db.TriagemModelos
            .Include(t => t.Perguntas)
            .Include(t => t.Faixas)
            .Where(t => t.Id == triagemModeloId && t.Ativa)
            .Where(VisivelPara(usuarioId))
            .FirstOrDefaultAsync();

        if (modelo is null) return (null, "Triagem não encontrada.");
        if (string.IsNullOrWhiteSpace(req.NomePaciente)) return (null, "Informe o nome da pessoa avaliada.");
        if (req.Idade is < 0 or > 130) return (null, "Idade inválida.");
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId)) return (null, "Usuário não encontrado.");

        var perguntasPorId = modelo.Perguntas.ToDictionary(p => p.Id);
        var pontuacao = 0;
        var respostas = new List<RespostaDada>();

        foreach (var r in req.Respostas)
        {
            if (!perguntasPorId.TryGetValue(r.PerguntaId, out var pergunta))
                return (null, $"Pergunta {r.PerguntaId} não pertence a esta triagem.");

            if (r.Valor) pontuacao += pergunta.Peso;
            respostas.Add(new RespostaDada { PerguntaId = r.PerguntaId, Valor = r.Valor });
        }

        var pontuacaoMaxima = modelo.Perguntas.Sum(p => p.Peso);

        var faixa = modelo.Faixas
            .OrderBy(f => f.Ordem)
            .FirstOrDefault(f => pontuacao >= f.PontuacaoMin && pontuacao <= f.PontuacaoMax)
            ?? modelo.Faixas.OrderBy(f => f.Ordem).LastOrDefault();

        var resultado = new TriagemResultado
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
            Respostas = respostas
        };

        db.TriagemResultados.Add(resultado);
        await db.SaveChangesAsync();

        return (new ResultadoResponse(
            resultado.Id, modelo.Id, modelo.Titulo,
            resultado.NomePaciente, resultado.Idade, resultado.Sexo,
            resultado.Pontuacao, resultado.PontuacaoMaxima,
            resultado.Classificacao, resultado.Recomendacao, resultado.Cor, resultado.Data), null);
    }

    private const int TamanhoPaginaPadrao = 100;
    private const int TamanhoPaginaMaximo = 200;

    /// <summary>
    /// Histórico do usuário, paginado (mais recentes primeiro) para evitar que a
    /// consulta e a resposta cresçam sem limite conforme o histórico aumenta.
    /// </summary>
    public async Task<List<HistoricoItem>> HistoricoAsync(
        int usuarioId, int? triagemModeloId = null, int pagina = 1, int tamanhoPagina = TamanhoPaginaPadrao)
    {
        pagina = Math.Max(pagina, 1);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, TamanhoPaginaMaximo);

        // Sem Include: a projeção Select abaixo já referencia r.TriagemModelo.Titulo,
        // o que faz o EF Core gerar o JOIN necessário sozinho. Include aqui não muda o
        // SQL gerado (é ignorado quando há projeção) — só sugeria uma materialização
        // que não ocorre.
        var query = db.TriagemResultados
            .AsNoTracking()
            .Where(r => r.UsuarioId == usuarioId);

        if (triagemModeloId is not null)
            query = query.Where(r => r.TriagemModeloId == triagemModeloId);

        var resultado = await query
            .OrderByDescending(r => r.Data)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(r => new HistoricoItem(
                r.Id, r.TriagemModeloId, r.TriagemModelo!.Titulo,
                r.NomePaciente, r.Idade, r.Sexo,
                r.Pontuacao, r.PontuacaoMaxima, r.Classificacao,
                r.Cor, r.Data))
            .ToListAsync();

        // Trilha de auditoria mínima: quem acessou dados de pacientes (nome + respostas
        // de saúde), quando e quantos registros — sem logar o conteúdo sensível em si.
        logger.LogInformation(
            "Usuário {UsuarioId} acessou o histórico (triagemModeloId={TriagemModeloId}, página={Pagina}, {Quantidade} registro(s))",
            usuarioId, triagemModeloId, pagina, resultado.Count);

        return resultado;
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
                Cor = string.IsNullOrWhiteSpace(f.Cor) ? CorPadrao(i) : f.Cor!,
                Ordem = i + 1
            }).ToList();

    // ---------------- Validação ----------------

    private static string? ValidarModelo(string titulo, List<PerguntaInput> perguntas, List<FaixaInput> faixas)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return "Informe o título da triagem.";
        if (perguntas is null || perguntas.Count == 0) return "Adicione pelo menos uma pergunta.";
        if (perguntas.Count > 50) return "Máximo de 50 perguntas por triagem.";
        if (perguntas.Any(p => string.IsNullOrWhiteSpace(p.Texto))) return "Toda pergunta precisa de um texto.";
        if (perguntas.Any(p => p.Peso is < 1 or > 100)) return "O peso de cada pergunta deve estar entre 1 e 100.";
        if (faixas is null || faixas.Count < 2) return "Defina pelo menos duas faixas de resultado.";
        if (faixas.Any(f => string.IsNullOrWhiteSpace(f.Titulo))) return "Toda faixa de resultado precisa de um título.";
        if (faixas.Any(f => f.PontuacaoMin > f.PontuacaoMax)) return "Em cada faixa, a pontuação mínima deve ser menor ou igual à máxima.";
        if (faixas.Any(f => !string.IsNullOrWhiteSpace(f.Cor) && !CorHexValida.IsMatch(f.Cor)))
            return "A cor de cada faixa deve ser um hexadecimal válido (ex.: #10B981).";

        var ordenadas = faixas.OrderBy(f => f.PontuacaoMin).ToList();
        for (var i = 1; i < ordenadas.Count; i++)
        {
            if (ordenadas[i].PontuacaoMin <= ordenadas[i - 1].PontuacaoMax)
                return "As faixas de resultado não podem se sobrepor.";
        }

        var pesoTotal = perguntas.Sum(p => p.Peso);
        if (ordenadas[0].PontuacaoMin > 0)
            return "A primeira faixa deve começar em 0.";
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
}
