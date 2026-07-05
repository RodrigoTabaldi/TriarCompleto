using Microsoft.EntityFrameworkCore;
using Triagem.API.Data;
using Triagem.API.Dtos;
using Triagem.API.Models;

namespace Triagem.API.Services;

public class TriagemService(TriagemDbContext db, CacheService cache, ILogger<TriagemService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private Task InvalidateCacheAsync() => cache.BumpVersionAsync();

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

    public async Task<TriagemModeloDetalhe?> ObterDetalheAsync(int id)
    {
        var versao = await cache.GetVersionAsync();
        var chave = $"triar:triagens:v{versao}:detalhe:{id}";
        return await cache.GetOrCreateAsync(chave, CacheTtl, async () =>
        {
            var t = await db.TriagemModelos
                .AsNoTracking()
                .Include(x => x.Perguntas)
                .Include(x => x.Faixas)
                .FirstOrDefaultAsync(x => x.Id == id && x.Ativa);

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

    public async Task<(TriagemModeloDetalhe? Detalhe, string? Erro)> CriarAsync(CriarTriagemRequest req)
    {
        var erro = ValidarModelo(req.Titulo, req.Perguntas, req.Faixas);
        if (erro is not null) return (null, erro);

        if (!await db.Usuarios.AnyAsync(u => u.Id == req.UsuarioId))
            return (null, "Usuário não encontrado.");

        var modelo = new TriagemModelo
        {
            Titulo = req.Titulo.Trim(),
            PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo.Trim(),
            Descricao = req.Descricao?.Trim() ?? "",
            Icone = string.IsNullOrWhiteSpace(req.Icone) ? "📋" : req.Icone.Trim(),
            CriadorUsuarioId = req.UsuarioId,
            Perguntas = req.Perguntas
                .Select((p, i) => new Pergunta { Texto = p.Texto.Trim(), Peso = p.Peso, Ordem = i + 1 })
                .ToList(),
            Faixas = req.Faixas
                .OrderBy(f => f.PontuacaoMin)
                .Select((f, i) => new FaixaResultado
                {
                    Titulo = f.Titulo.Trim(),
                    Recomendacao = f.Recomendacao?.Trim() ?? "",
                    PontuacaoMin = f.PontuacaoMin,
                    PontuacaoMax = f.PontuacaoMax,
                    Cor = string.IsNullOrWhiteSpace(f.Cor) ? CorPadrao(i) : f.Cor!,
                    Ordem = i + 1
                }).ToList()
        };

        db.TriagemModelos.Add(modelo);

        // triagem criada pelo usuário entra visível na home
        modelo.Ativa = true;
        await db.SaveChangesAsync();

        db.UsuarioTriagensHome.Add(new UsuarioTriagemHome
        {
            UsuarioId = req.UsuarioId,
            TriagemModeloId = modelo.Id,
            Visivel = true,
            Ordem = 999
        });
        await db.SaveChangesAsync();

        await InvalidateCacheAsync();
        logger.LogInformation("Usuário {UsuarioId} criou a triagem {TriagemId} ({Titulo})",
            req.UsuarioId, modelo.Id, modelo.Titulo);

        return (await ObterDetalheAsync(modelo.Id), null);
    }

    public async Task<(bool Ok, string? Erro)> AtualizarAsync(int id, CriarTriagemRequest req)
    {
        var erro = ValidarModelo(req.Titulo, req.Perguntas, req.Faixas);
        if (erro is not null) return (false, erro);

        var modelo = await db.TriagemModelos
            .Include(t => t.Perguntas)
            .Include(t => t.Faixas)
            .FirstOrDefaultAsync(t => t.Id == id && t.Ativa);

        if (modelo is null) return (false, "Triagem não encontrada.");
        if (modelo.CriadorUsuarioId != req.UsuarioId)
            return (false, "Apenas o criador pode editar esta triagem.");

        modelo.Titulo = req.Titulo.Trim();
        modelo.PublicoAlvo = string.IsNullOrWhiteSpace(req.PublicoAlvo) ? "Todas as idades" : req.PublicoAlvo.Trim();
        modelo.Descricao = req.Descricao?.Trim() ?? "";
        modelo.Icone = string.IsNullOrWhiteSpace(req.Icone) ? modelo.Icone : req.Icone.Trim();

        db.Perguntas.RemoveRange(modelo.Perguntas);
        db.FaixasResultado.RemoveRange(modelo.Faixas);
        modelo.Perguntas = req.Perguntas
            .Select((p, i) => new Pergunta { Texto = p.Texto.Trim(), Peso = p.Peso, Ordem = i + 1 })
            .ToList();
        modelo.Faixas = req.Faixas
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

        await db.SaveChangesAsync();
        await InvalidateCacheAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Erro)> DesativarAsync(int id, int usuarioId)
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

    public async Task ConfigurarHomeAsync(int usuarioId, ConfigurarHomeRequest req)
    {
        var existentes = await db.UsuarioTriagensHome
            .Where(h => h.UsuarioId == usuarioId)
            .ToDictionaryAsync(h => h.TriagemModeloId);

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

        await db.SaveChangesAsync();
        await InvalidateCacheAsync();
    }

    // ---------------- Execução ----------------

    public async Task<(ResultadoResponse? Resultado, string? Erro)> ResponderAsync(int triagemModeloId, ResponderTriagemRequest req)
    {
        var modelo = await db.TriagemModelos
            .Include(t => t.Perguntas)
            .Include(t => t.Faixas)
            .FirstOrDefaultAsync(t => t.Id == triagemModeloId && t.Ativa);

        if (modelo is null) return (null, "Triagem não encontrada.");
        if (string.IsNullOrWhiteSpace(req.NomePaciente)) return (null, "Informe o nome da pessoa avaliada.");
        if (req.Idade is < 0 or > 130) return (null, "Idade inválida.");
        if (!await db.Usuarios.AnyAsync(u => u.Id == req.UsuarioId)) return (null, "Usuário não encontrado.");

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
            UsuarioId = req.UsuarioId,
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

    public async Task<List<HistoricoItem>> HistoricoAsync(int usuarioId, int? triagemModeloId = null)
    {
        var query = db.TriagemResultados
            .AsNoTracking()
            .Include(r => r.TriagemModelo)
            .Where(r => r.UsuarioId == usuarioId);

        if (triagemModeloId is not null)
            query = query.Where(r => r.TriagemModeloId == triagemModeloId);

        return await query
            .OrderByDescending(r => r.Data)
            .Select(r => new HistoricoItem(
                r.Id, r.TriagemModeloId, r.TriagemModelo!.Titulo,
                r.NomePaciente, r.Idade, r.Sexo,
                r.Pontuacao, r.PontuacaoMaxima, r.Classificacao, r.Classificacao,
                r.Cor, r.Data))
            .ToListAsync();
    }

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
