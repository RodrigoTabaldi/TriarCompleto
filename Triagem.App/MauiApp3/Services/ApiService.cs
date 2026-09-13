using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
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
    private static bool _forcarModoLocalIndividual;
    private static int _versaoTriagens;
    private static int _versaoHistorico;

    public static int VersaoTriagens => Volatile.Read(ref _versaoTriagens);
    public static int VersaoHistorico => Volatile.Read(ref _versaoHistorico);

    /// <summary>
    /// Modo local (demonstração): o app não fala com API nenhuma e usa o
    /// <see cref="BancoLocal"/> (SQLite no próprio aparelho) como fonte de dados.
    /// É ligado em tempo de compilação com <c>-p:TriarModoLocal=true</c>, que define a
    /// constante MODO_LOCAL — o mesmo código-fonte gera tanto o APK de demonstração
    /// offline quanto o app normal que consome a API.
    /// </summary>
    public static bool ModoLocal =>
#if MODO_LOCAL
        _forcarModoLocalIndividual || true
#else
        _forcarModoLocalIndividual
#endif
        ;

    /// <summary>
    /// Id do usuário autenticado nesta sessão. No modo local faz o papel que o JWT faz
    /// contra a API: é a identidade usada para filtrar triagens e histórico, em vez de
    /// confiar em um id enviado pela tela.
    /// </summary>
    private static int _usuarioAtualId;

    // ⚠️ PRODUÇÃO: troque pela URL pública HTTPS da sua API .NET (ex.: Azure/VPS).
    // Num celular real (instalado via Firebase), "localhost" é o próprio telefone —
    // por isso o build de Release precisa apontar para um endereço público de verdade.
    private static readonly string UrlProducao = typeof(ApiService).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "TriarApiBaseUrl")?.Value ?? "";

    // IP do PC na rede local — usado só quando o app roda num celular Android físico
    // (não no emulador) em builds DEBUG, para alcançar a API rodando no PC pela Wi-Fi.
    // Precisa estar na mesma rede, e é preciso trocar aqui se o IP do PC mudar.
    private const string UrlDevRedeLocal = "http://192.168.1.19:5036";

    /// <summary>
    /// Endpoint da API. Em DEBUG usa o ambiente local (emulador ou dispositivo físico
    /// na mesma rede); em RELEASE usa a URL de produção. Pode ser sobrescrito em
    /// runtime via <see cref="BaseUrl"/>.
    /// </summary>
    public static string BaseUrl { get; set; } =
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android
            ? (DeviceInfo.DeviceType == DeviceType.Virtual ? "http://10.0.2.2:5036" : UrlDevRedeLocal)
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
        // No modo local não existe API para configurar.
        if (ModoLocal) return;

#if !DEBUG
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "A URL pública HTTPS da API não foi incorporada ao build. " +
                "Compile com -p:TriarApiBaseUrl=https://sua-api (veja deploy/firebase/README.md).");
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
        _forcarModoLocalIndividual = false;
        DefinirToken(null);
        LimparCache();
        _usuarioAtualId = 0;
        LimparSessaoPersistida();
    }

    public static bool EhSessaoExpirada(Exception ex) =>
        ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized };

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
        await SecureStorage.Default.SetAsync(ChaveUsuarioId, usuario.Id.ToString(CultureInfo.InvariantCulture));
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
                LimparSessaoPersistida();
                return null;
            }

            var idTexto = await SecureStorage.Default.GetAsync(ChaveUsuarioId);
            if (!int.TryParse(idTexto, out var id))
                return null;

            // No modo local a sessão só vale se o usuário ainda existir no banco do
            // aparelho: limpar os dados do app apaga o banco, mas não o SecureStorage.
            if (ModoLocal && !await BancoLocal.UsuarioExisteAsync(id))
            {
                LimparSessaoPersistida();
                return null;
            }

            DefinirToken(token);
            _usuarioAtualId = id;
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

    private static void LimparSessaoPersistida()
    {
        SecureStorage.Default.Remove(ChaveToken);
        SecureStorage.Default.Remove(ChaveExpiraEm);
        SecureStorage.Default.Remove(ChaveUsuarioId);
        SecureStorage.Default.Remove(ChaveUsuarioNome);
        SecureStorage.Default.Remove(ChaveUsuarioEmail);
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
    private sealed record AuthResponse(int Id, string Nome, string Email, string Token, DateTime ExpiraEm);

    public static async Task<Usuario?> LoginAsync(string email, string senha)
    {
        if (ModoLocal)
            return await IniciarSessaoLocalAsync(await BancoLocal.LoginAsync(email, senha));

        using var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/auth/login", new { email, senha }, JsonOptions);
        if (!resp.IsSuccessStatusCode) return null;
        return await AutenticarAsync(resp);
    }

    public static async Task<(Usuario? Usuario, string? Erro)> RegistrarAsync(string nome, string email, string senha)
    {
        if (ModoLocal)
        {
            var (novo, erroLocal) = await BancoLocal.RegistrarAsync(nome, email, senha);
            return (await IniciarSessaoLocalAsync(novo), erroLocal);
        }

        using var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/auth/register", new { nome, email, senha }, JsonOptions);
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
        _usuarioAtualId = usuario.Id;
        await PersistirSessaoAsync(usuario, auth.Token, auth.ExpiraEm);
        return usuario;
    }

    /// <summary>
    /// Equivalente local do <see cref="AutenticarAsync"/>: não há JWT para guardar, mas
    /// a sessão continua sendo persistida para o usuário não precisar logar de novo a
    /// cada abertura do app.
    /// </summary>
    private static async Task<Usuario?> IniciarSessaoLocalAsync(Usuario? usuario)
    {
        if (usuario is null) return null;

        LimparCache();
        _usuarioAtualId = usuario.Id;
        await PersistirSessaoAsync(usuario, TokenSessaoLocal, DateTime.UtcNow.AddDays(365));
        return usuario;
    }

    /// <summary>Marcador gravado no lugar do JWT quando não há API. Nunca sai do aparelho.</summary>
    private const string TokenSessaoLocal = "sessao-local";

    private const string ChaveUsuarioIndividualId = "triar_individual_usuario_id";

    public static async Task<Usuario> IniciarModoIndividualAsync()
    {
        DefinirToken(null);
        _forcarModoLocalIndividual = true;
        LimparCache();

        var idSalvo = await SecureStorage.Default.GetAsync(ChaveUsuarioIndividualId);
        if (int.TryParse(idSalvo, out var id))
        {
            var existente = await BancoLocal.ObterUsuarioAsync(id);
            if (existente is not null)
            {
                _usuarioAtualId = existente.Id;
                return existente;
            }
        }

        var usuario = await BancoLocal.CriarUsuarioIndividualAsync();
        _usuarioAtualId = usuario.Id;
        await SecureStorage.Default.SetAsync(ChaveUsuarioIndividualId, usuario.Id.ToString(CultureInfo.InvariantCulture));
        return usuario;
    }

    // ---------------- Triagens ----------------
    public static void PrepararModoGrupo()
    {
        _forcarModoLocalIndividual = false;
        _usuarioAtualId = 0;
        DefinirToken(null);
        LimparCache();
    }

    public static async Task<List<TriagemResumo>> ListarTriagensAsync(int usuarioId, bool forceRefresh = false)
    {
        // O cache existe para poupar chamadas de rede; contra o banco local ele só
        // criaria leituras desatualizadas logo depois de criar ou editar uma triagem.
        if (ModoLocal) return await BancoLocal.ListarTriagensAsync(usuarioId);

        var cacheKey = $"triagens_{usuarioId}";
        if (forceRefresh) Cache.TryRemove(cacheKey, out _);
        var cached = GetCache<List<TriagemResumo>>(cacheKey);
        if (cached is not null) return cached;

        // A API deriva o usuário sempre do token (nunca de um id na URL); usuarioId
        // aqui só serve para a chave de cache local e o caminho ModoLocal acima.
        var result = await Http.GetFromJsonAsync<List<TriagemResumo>>(
            $"{BaseUrl}/api/triagens", JsonOptions) ?? [];
        SetCache(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public static async Task<TriagemDetalhe?> ObterTriagemAsync(int id)
    {
        if (ModoLocal) return await BancoLocal.ObterTriagemAsync(_usuarioAtualId, id);

        var cacheKey = $"triagem_{id}";
        var cached = GetCache<TriagemDetalhe>(cacheKey);
        if (cached is not null) return cached;

        var result = await Http.GetFromJsonAsync<TriagemDetalhe>($"{BaseUrl}/api/triagens/{id}", JsonOptions);
        if (result is not null) SetCache(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public static async Task<(bool Ok, string? Erro)> CriarTriagemAsync(CriarTriagemPayload payload)
    {
        if (ModoLocal)
        {
            var resultadoLocal = await BancoLocal.CriarTriagemAsync(_usuarioAtualId, payload);
            if (resultadoLocal.Ok) Interlocked.Increment(ref _versaoTriagens);
            return resultadoLocal;
        }

        using var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/triagens", payload, JsonOptions);
        if (resp.IsSuccessStatusCode)
        {
            InvalidarCache("triagens_", "historico_");
            Interlocked.Increment(ref _versaoTriagens);
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public static async Task<(bool Ok, string? Erro)> AtualizarTriagemAsync(int id, CriarTriagemPayload payload)
    {
        if (ModoLocal)
        {
            var resultadoLocal = await BancoLocal.AtualizarTriagemAsync(_usuarioAtualId, id, payload);
            if (resultadoLocal.Ok)
            {
                Interlocked.Increment(ref _versaoTriagens);
                Interlocked.Increment(ref _versaoHistorico);
            }
            return resultadoLocal;
        }

        using var resp = await Http.PutAsJsonAsync($"{BaseUrl}/api/triagens/{id}", payload, JsonOptions);
        if (resp.IsSuccessStatusCode)
        {
            Cache.TryRemove($"triagem_{id}", out _);
            InvalidarCache("triagens_", "historico_");
            Interlocked.Increment(ref _versaoTriagens);
            Interlocked.Increment(ref _versaoHistorico);
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public static async Task<(bool Ok, string? Erro)> ExcluirTriagemAsync(int id)
    {
        if (ModoLocal)
        {
            var resultadoLocal = await BancoLocal.ExcluirTriagemAsync(_usuarioAtualId, id);
            if (resultadoLocal.Ok) Interlocked.Increment(ref _versaoTriagens);
            return resultadoLocal;
        }

        using var resp = await Http.DeleteAsync($"{BaseUrl}/api/triagens/{id}");
        if (resp.IsSuccessStatusCode)
        {
            Cache.TryRemove($"triagem_{id}", out _);
            InvalidarCache("triagens_", "historico_");
            Interlocked.Increment(ref _versaoTriagens);
            return (true, null);
        }
        return (false, await resp.Content.ReadAsStringAsync());
    }

    // ---------------- Execução ----------------

    public static async Task<(ResultadoTriagem? Resultado, string? Erro)> ResponderAsync(int triagemId, ResponderTriagemPayload payload)
    {
        if (ModoLocal)
        {
            var resultadoLocal = await BancoLocal.ResponderAsync(_usuarioAtualId, triagemId, payload);
            if (resultadoLocal.Resultado is not null) Interlocked.Increment(ref _versaoHistorico);
            return resultadoLocal;
        }

        using var resp = await Http.PostAsJsonAsync($"{BaseUrl}/api/triagens/{triagemId}/responder", payload, JsonOptions);
        if (!resp.IsSuccessStatusCode)
            return (null, await resp.Content.ReadAsStringAsync());

        // novo resultado gravado: invalida o histórico em cache
        InvalidarCache("historico_");
        Interlocked.Increment(ref _versaoHistorico);

        return (await resp.Content.ReadFromJsonAsync<ResultadoTriagem>(JsonOptions), null);
    }

    // ---------------- Histórico ----------------

    public static async Task<List<HistoricoItem>> HistoricoPaginaAsync(
        int usuarioId, int? triagemId = null, int pagina = 1, int tamanhoPagina = 50)
    {
        pagina = Math.Max(1, pagina);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, 200);
        if (ModoLocal)
            return await BancoLocal.HistoricoAsync(usuarioId, triagemId, pagina, tamanhoPagina);

        var cacheKey = triagemId is not null
            ? $"historico_{usuarioId}_{triagemId}_{pagina}_{tamanhoPagina}"
            : $"historico_{usuarioId}_{pagina}_{tamanhoPagina}";

        var cached = GetCache<List<HistoricoItem>>(cacheKey);
        if (cached is not null) return cached;

        var query = triagemId is not null ? $"triagemModeloId={triagemId}&" : "";
        var url = $"{BaseUrl}/api/triagem/usuario/{usuarioId}?{query}pagina={pagina}&tamanhoPagina={tamanhoPagina}";
        var result = await Http.GetFromJsonAsync<List<HistoricoItem>>(url, JsonOptions) ?? [];
        SetCache(cacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    // ---------------- Home ----------------

    public static async Task ConfigurarHomeAsync(int usuarioId, IEnumerable<(int TriagemModeloId, bool Visivel, int Ordem)> itens)
    {
        if (ModoLocal)
        {
            await BancoLocal.ConfigurarHomeAsync(usuarioId, itens);
            Interlocked.Increment(ref _versaoTriagens);
            return;
        }

        var payload = new
        {
            itens = itens.Select(i => new { triagemModeloId = i.TriagemModeloId, visivel = i.Visivel, ordem = i.Ordem })
        };
        using var resp = await Http.PutAsJsonAsync($"{BaseUrl}/api/usuarios/home", payload, JsonOptions);
        resp.EnsureSuccessStatusCode();
        InvalidarCache("triagens_");
        Interlocked.Increment(ref _versaoTriagens);
    }

    public static async Task<string> ExportarDadosJsonAsync()
    {
        if (ModoLocal) return await BancoLocal.ExportarDadosJsonAsync(_usuarioAtualId);
        return await Http.GetStringAsync($"{BaseUrl}/api/usuarios/me/export");
    }

    public static async Task<(bool Ok, string? Erro)> ExcluirContaAsync(string senha)
    {
        if (ModoLocal) return await BancoLocal.ExcluirContaAsync(_usuarioAtualId, senha);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/api/usuarios/me")
        {
            Content = JsonContent.Create(new { senha }, options: JsonOptions)
        };
        using var response = await Http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? (true, null)
            : (false, await response.Content.ReadAsStringAsync());
    }
}
