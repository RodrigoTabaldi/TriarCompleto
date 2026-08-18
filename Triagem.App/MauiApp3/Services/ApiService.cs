using System.Collections.Concurrent;
using System.Net.Http.Headers;
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

    // ⚠️ PRODUÇÃO: troque pela URL pública HTTPS da sua API .NET (ex.: Azure/VPS).
    // Num celular real (instalado via Firebase), "localhost" é o próprio telefone —
    // por isso o build de Release precisa apontar para um endereço público de verdade.
    private const string UrlProducao = "https://SUA-API-DE-PRODUCAO.com";
    private const string MarcadorPlaceholder = "SUA-API-DE-PRODUCAO";

    /// <summary>
    /// Endpoint da API. Em DEBUG usa o ambiente local; em RELEASE usa a URL de produção.
    /// Pode ser sobrescrito em runtime via <see cref="BaseUrl"/>.
    /// </summary>
    public static string BaseUrl { get; set; } =
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5036"
            : "http://localhost:5036";
#else
        UrlProducao;
#endif

    /// <summary>
    /// Falha alto e cedo (na inicialização) se um build de Release for compilado sem
    /// substituir <see cref="UrlProducao"/> pela URL real. Sem isso, o app compilava
    /// silenciosamente e só falhava mais tarde, na tela de login, com uma mensagem de
    /// rede genérica difícil de associar à causa real.
    /// </summary>
    public static void GarantirConfiguradoEmRelease()
    {
#if !DEBUG
        if (BaseUrl.Contains(MarcadorPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ApiService.UrlProducao ainda é o placeholder de exemplo. " +
                "Configure a URL pública HTTPS real da API antes de gerar um build de Release " +
                "(veja deploy/firebase/README.md, Passo 0).");
        }
#endif
    }

    /// <summary>Token JWT da sessão. Aplicado como Bearer em toda chamada autenticada.</summary>
    public static void DefinirToken(string? token)
    {
        Http.DefaultRequestHeaders.Authorization =
            string.IsNullOrWhiteSpace(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Encerra a sessão: limpa token, cache local e a sessão persistida no dispositivo.</summary>
    public static void Logout()
    {
        DefinirToken(null);
        LimparCache();
        SecureStorage.Default.RemoveAll();
    }

    // ---------------- Sessão persistida (SecureStorage) ----------------
    // O token e os dados do usuário logado são salvos no armazenamento seguro do
    // dispositivo (Keychain/Keystore/DPAPI conforme a plataforma) para que o usuário
    // não precise logar de novo toda vez que o app é fechado e reaberto.

    private const string ChaveToken = "triar_token";
    private const string ChaveExpiraEm = "triar_expira_em";
    private const string ChaveUsuarioId = "triar_usuario_id";
    private const string ChaveUsuarioNome = "triar_usuario_nome";
    private const string ChaveUsuarioEmail = "triar_usuario_email";

    private static async Task PersistirSessaoAsync(Usuario usuario, string token, DateTime expiraEm)
    {
        await SecureStorage.Default.SetAsync(ChaveToken, token);
        await SecureStorage.Default.SetAsync(ChaveExpiraEm, expiraEm.ToString("O"));
        await SecureStorage.Default.SetAsync(ChaveUsuarioId, usuario.Id.ToString());
        await SecureStorage.Default.SetAsync(ChaveUsuarioNome, usuario.Nome ?? "");
        await SecureStorage.Default.SetAsync(ChaveUsuarioEmail, usuario.Email ?? "");
    }

    /// <summary>
    /// Tenta restaurar a sessão salva no dispositivo. Retorna o usuário e já aplica o
    /// token em <see cref="Http"/> se houver uma sessão válida e não expirada; caso
    /// contrário, limpa qualquer resíduo e retorna null (o app volta para o login).
    /// </summary>
    public static async Task<Usuario?> RestaurarSessaoAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(ChaveToken);
            var expiraEmTexto = await SecureStorage.Default.GetAsync(ChaveExpiraEm);

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expiraEmTexto))
                return null;

            if (!DateTime.TryParse(expiraEmTexto, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var expiraEm) ||
                expiraEm <= DateTime.UtcNow)
            {
                SecureStorage.Default.RemoveAll();
                return null;
            }

            var idTexto = await SecureStorage.Default.GetAsync(ChaveUsuarioId);
            if (!int.TryParse(idTexto, out var id))
                return null;

            DefinirToken(token);
            return new Usuario
            {
                Id = id,
                Nome = await SecureStorage.Default.GetAsync(ChaveUsuarioNome),
                Email = await SecureStorage.Default.GetAsync(ChaveUsuarioEmail)
            };
        }
        catch
        {
            // Chave de criptografia do SecureStorage pode ter sido invalidada pelo SO
            // (ex.: restauração de backup em outro dispositivo) — trata como sessão
            // ausente em vez de derrubar a inicialização do app.
            return null;
        }
    }

    /// <summary>Remove do cache local as entradas cujas chaves começam por qualquer um dos prefixos.</summary>
    private static void InvalidarCache(params string[] prefixos)
    {
        foreach (var k in Cache.Keys.Where(k => prefixos.Any(k.StartsWith)).ToList())
            Cache.TryRemove(k, out _);
    }

    private static T? GetCache<T>(string key) where T : class
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

    /// <summary>Resposta de autenticação da API: usuário + token JWT.</summary>
    private record AuthResponse(int Id, string Nome, string Email, string Token, DateTime ExpiraEm);

    public static async Task<Usuario?> LoginAsync(string email, string senha)
    {
        var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/auth/login", new { email, senha }, JsonOptions);
        if (!resp.IsSuccessStatusCode) return null;
        return await AutenticarAsync(resp);
    }

    public static async Task<(Usuario? Usuario, string? Erro)> RegistrarAsync(string nome, string email, string senha)
    {
        var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/auth/register", new { nome, email, senha }, JsonOptions);
        if (!resp.IsSuccessStatusCode)
            return (null, await resp.Content.ReadAsStringAsync());
        return (await AutenticarAsync(resp), null);
    }

    private static async Task<Usuario?> AutenticarAsync(HttpResponseMessage resp)
    {
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        if (auth is null) return null;

        DefinirToken(auth.Token);
        LimparCache(); // sessão nova: descarta cache de qualquer usuário anterior
        var usuario = new Usuario { Id = auth.Id, Nome = auth.Nome, Email = auth.Email };
        await PersistirSessaoAsync(usuario, auth.Token, auth.ExpiraEm);
        return usuario;
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
            InvalidarCache("triagens_", "historico_");
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public static async Task<(bool Ok, string? Erro)> AtualizarTriagemAsync(int id, object payload)
    {
        var resp = await Http.PutAsJsonAsync($"{BaseUrl}/api/triagens/{id}", payload, JsonOptions);
        if (resp.IsSuccessStatusCode)
        {
            Cache.TryRemove($"triagem_{id}", out _);
            InvalidarCache("triagens_", "historico_");
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public static async Task<(bool Ok, string? Erro)> ExcluirTriagemAsync(int id)
    {
        var resp = await Http.DeleteAsync($"{BaseUrl}/api/triagens/{id}");
        if (resp.IsSuccessStatusCode)
        {
            Cache.TryRemove($"triagem_{id}", out _);
            InvalidarCache("triagens_", "historico_");
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
        InvalidarCache("historico_");

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
