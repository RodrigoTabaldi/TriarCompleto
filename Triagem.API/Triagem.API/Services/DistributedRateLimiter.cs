using StackExchange.Redis;

namespace Triagem.API.Services;

/// <summary>
/// Limitador de taxa de janela fixa apoiado em Redis (INCR + EXPIRE), para que o
/// limite seja compartilhado entre todas as instâncias da API atrás do load balancer.
///
/// Sem isto, o <see cref="Microsoft.AspNetCore.RateLimiting.RateLimiterOptions"/> em
/// memória do Program.cs conta por processo: com api1 e api2 atrás do nginx, um
/// cliente distribuído entre as duas obtém, na prática, o dobro do limite configurado.
/// Isso é tolerável na política geral, mas anula parte do propósito da política "auth"
/// (10 req/min por IP), pensada especificamente para dificultar força bruta de login.
///
/// Sem Redis configurado (dev local, LocalDB) há sempre um único processo, então o
/// limitador em memória já é exato — este serviço apenas deixa de atuar (retorna
/// sempre permitido) nesse caso. Qualquer falha de conexão com o Redis também abre o
/// limite (fail-open): um rate limiter não deve derrubar login por estar indisponível.
/// </summary>
public class DistributedRateLimiter(IConnectionMultiplexer? redis, ILogger<DistributedRateLimiter> logger)
{
    public async Task<bool> PermitirAsync(string chave, int limite, TimeSpan janela)
    {
        if (redis is null) return true;

        try
        {
            var db = redis.GetDatabase();
            var contagem = await db.StringIncrementAsync(chave);

            // Só a requisição que abre a janela (contagem == 1) define o TTL; as demais
            // apenas incrementam. Isso mantém a janela fixa a partir da primeira
            // requisição do IP, sem precisar de um script Lua para atomicidade extra.
            if (contagem == 1)
                await db.KeyExpireAsync(chave, janela);

            return contagem <= limite;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Rate limiter distribuído indisponível ({Chave}): {Erro}", chave, ex.Message);
            return true;
        }
    }
}
