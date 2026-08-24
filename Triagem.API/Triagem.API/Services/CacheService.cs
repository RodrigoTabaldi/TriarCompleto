using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Triagem.API.Services;

/// <summary>
/// Cache distribuído (Redis quando configurado, memória como fallback) com
/// serialização JSON e invalidação por versão — a versão vive no próprio cache,
/// então um bump feito pela api1 invalida também as leituras da api2.
/// Qualquer falha do Redis degrada para acesso direto ao banco: cache nunca derruba requisição.
/// </summary>
public class CacheService(IDistributedCache cache, ILogger<CacheService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string VersionKey = "triar:triagens:version";

    /// <summary>Token atual da geração do cache de triagens.</summary>
    public async Task<string> GetVersionAsync()
    {
        try
        {
            var raw = await cache.GetStringAsync(VersionKey);
            return raw ?? "0";
        }
        catch (Exception ex)
        {
            logger.LogWarning("Cache indisponível ao ler versão: {Erro}", ex.Message);
            // Token único impede reutilizar uma entrada potencialmente velha quando o
            // cache voltar durante a requisição.
            return $"nocache-{Guid.NewGuid():N}";
        }
    }

    /// <summary>Invalida todas as entradas versionadas (listas e detalhes de triagens).</summary>
    public async Task BumpVersionAsync()
    {
        try
        {
            // Uma nova geração aleatória exige uma única escrita. Diferentemente de
            // ler+incrementar, duas instâncias concorrentes nunca reutilizam a geração
            // publicada pela outra e nenhuma entrada intermediária volta a ser visível.
            await cache.SetStringAsync(VersionKey, Guid.NewGuid().ToString("N"));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Cache indisponível ao invalidar: {Erro}", ex.Message);
        }
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T?>> factory)
    {
        try
        {
            var raw = await cache.GetStringAsync(key);
            if (raw is not null)
                return JsonSerializer.Deserialize<T>(raw, Json);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Cache indisponível (get {Chave}): {Erro}", key, ex.Message);
            return await factory();
        }

        var valor = await factory();
        if (valor is null) return valor;

        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(valor, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        }
        catch (Exception ex)
        {
            logger.LogWarning("Cache indisponível (set {Chave}): {Erro}", key, ex.Message);
        }

        return valor;
    }
}
