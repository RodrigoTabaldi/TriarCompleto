using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using MauiApp3.Models;

namespace MauiApp3.Services;

/// <summary>Cliente central da Triar API. Um único HttpClient para o app inteiro.</summary>
public static class ApiService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Cache thread-safe com expiração (5 min para triagens, 10 min para histórico)
    private static readonly ConcurrentDictionary<string, (object Data, DateTime ExpiresAt)> Cache = new();

    /// <summary>Emulador Android enxerga o host como 10.0.2.2; demais plataformas usam localhost.</summary>
    public static string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5036"
            : "http://localhost:5036";

    private static T? GetCache<T>(string key)
    {
        if (Cache.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.ExpiresAt)
            return (T)entry.Data;
        Cache.TryRemove(key, out _);
        return null;
    }

    private static void SetCache<T>(string key, T data, TimeSpan ttl)
    {
        Cache[key] = (data!, DateTime.UtcNow.Add(ttl));
    }

    public static void LimparCache()
    {
        Cache.Clear();
    }

    // ---------------- Auth ----------------

    public static async Task<Usuario?> LoginAsync(string email, string senha)
    {
        var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/auth/login", new { email, senha }, JsonOptions);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<Usuario>(JsonOptions);
    }

    public static async Task<(bool Ok, string? Erro)> RegistrarAsync(string nome, string email, string senha)
    {
        var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/auth/register", new { nome, email, senha }, JsonOptions);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync());
    }

    // ---------------- Triagens ----------------

    public static async Task<List<TriagemResumo>> ListarTriagensAsync(int usuarioId)
    {
        var cacheKey = $"triagens_{usuarioId}";
        var cached = GetCache<List<TriagemResumo>>(cacheKey);
        if (cached is not null) return cached;

        var result = await Http.GetFromJsonAsync<List<TriagemResumo>>(
            $"{BaseUrl}/api/triagens?usuarioId={usuarioId}", JsonOptions) ?? [];
        SetCache(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public static async Task<TriagemDetalhe?> ObterTriagemAsync(int id)
    {
        var cacheKey = $"triagem_{id}";
        var cached = GetCache<TriagemDetalhe>(cacheKey);
        if (cached is not null) return cached;

        var result = await Http.GetFromJsonAsync<TriagemDetalhe>($"{BaseUrl}/api/triagens/{id}", JsonOptions);
        if (result is not null) SetCache(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public static async Task<(bool Ok, string? Erro)> CriarTriagemAsync(object payload)
    {
        var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/triagens", payload, JsonOptions);
        if (resp.IsSuccessStatusCode)
        {
            // Invalidar cache de listagem (será refeito no próximo acesso)
            foreach (var k in Cache.Keys.Where(k => k.StartsWith("triagens_") || k.StartsWith("historico_")).ToList()) Cache.TryRemove(k, out _);
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public static async Task<(bool Ok, string? Erro)> AtualizarTriagemAsync(int id, object payload)
    {
        var resp = await Http.PutAsJsonAsync($"{BaseUrl}/api/triagens/{id}", payload, JsonOptions);
        if (resp.IsSuccessStatusCode)
        {
            // Invalidar cache de detalhe e listagem
            Cache.TryRemove($"triagem_{id}", out _);
            foreach (var k in Cache.Keys.Where(k => k.StartsWith("triagens_") || k.StartsWith("historico_")).ToList()) Cache.TryRemove(k, out _);
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public static async Task<(bool Ok, string? Erro)> ExcluirTriagemAsync(int id, int usuarioId)
    {
        var resp = await Http.DeleteAsync($"{BaseUrl}/api/triagens/{id}?usuarioId={usuarioId}");
        if (resp.IsSuccessStatusCode)
        {
            // Invalidar cache de detalhe e listagem
            Cache.TryRemove($"triagem_{id}", out _);
            foreach (var k in Cache.Keys.Where(k => k.StartsWith("triagens_") || k.StartsWith("historico_")).ToList()) Cache.TryRemove(k, out _);
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    // ---------------- Execução ----------------

    public static async Task<(ResultadoTriagem? Resultado, string? Erro)> ResponderAsync(int triagemId, object payload)
    {
        var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/triagens/{triagemId}/responder", payload, JsonOptions);
        if (!resp.IsSuccessStatusCode)
            return (null, await resp.Content.ReadAsStringAsync());

        // novo resultado gravado: invalida o histórico em cache
        foreach (var k in Cache.Keys.Where(k => k.StartsWith("historico_")).ToList())
            Cache.TryRemove(k, out _);

        return (await resp.Content.ReadFromJsonAsync<ResultadoTriagem>(JsonOptions), null);
    }

    // ---------------- Histórico ----------------

    public static async Task<List<HistoricoItem>> HistoricoAsync(int usuarioId, int? triagemId = null)
    {
        var cacheKey = triagemId is not null
            ? $"historico_{usuarioId}_{triagemId}"
            : $"historico_{usuarioId}";

        var cached = GetCache<List<HistoricoItem>>(cacheKey);
        if (cached is not null) return cached;

        var url = $"{BaseUrl}/api/triagem/usuario/{usuarioId}";
        if (triagemId is not null) url += $"?triagemModeloId={triagemId}";

        var result = await Http.GetFromJsonAsync<List<HistoricoItem>>(url, JsonOptions) ?? [];
        SetCache(cacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    // ---------------- Home ----------------

    public static async Task ConfigurarHomeAsync(int usuarioId, IEnumerable<(int TriagemModeloId, bool Visivel, int Ordem)> itens)
    {
        var payload = new
        {
            itens = itens.Select(i => new { triagemModeloId = i.TriagemModeloId, visivel = i.Visivel, ordem = i.Ordem })
        };
        var resp = await Http.PutAsJsonAsync($"{BaseUrl}/api/usuarios/{usuarioId}/home", payload, JsonOptions);
        resp.EnsureSuccessStatusCode();
    }
}
