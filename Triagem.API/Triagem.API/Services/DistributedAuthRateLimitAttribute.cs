using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Triagem.API.Services;

/// <summary>
/// Reforço distribuído (via Redis) da política "auth" de rate limiting, aplicado nas
/// rotas de login/cadastro. O <c>[EnableRateLimiting("auth")]</c> do Program.cs
/// continua sendo a primeira linha de defesa (rápida, em memória); este filtro é a
/// checagem autoritativa entre instâncias — ver <see cref="DistributedRateLimiter"/>
/// para o porquê de as duas coexistirem.
/// </summary>
public class DistributedAuthRateLimitAttribute : Attribute, IAsyncActionFilter
{
    private const int Limite = 10;
    private static readonly TimeSpan Janela = TimeSpan.FromMinutes(1);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var limiter = context.HttpContext.RequestServices.GetRequiredService<DistributedRateLimiter>();
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var chave = $"triar:ratelimit:auth:{ip}";

        if (!await limiter.PermitirAsync(chave, Limite, Janela))
        {
            context.Result = new ObjectResult("Muitas requisições. Aguarde alguns segundos e tente novamente.")
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
            return;
        }

        await next();
    }
}
