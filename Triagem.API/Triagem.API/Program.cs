using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triagem.API.Data;
using Triagem.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Banco de dados (SQL Server) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

builder.Services.AddDbContext<TriagemDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
        sql.CommandTimeout(30);
    }));

// ---------- Serviços ----------
builder.Services.AddControllers();
builder.Services.AddScoped<TriagemService>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// ---------- Cache ----------
// Com "ConnectionStrings:Redis" configurada (ex.: docker-compose), o cache é
// distribuído via Redis — compartilhado entre api1 e api2, inclusive a invalidação.
// Sem Redis (ex.: rodando local com LocalDB), cai para cache em memória do processo.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = redisConnection;
        o.InstanceName = "triar:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSingleton<CacheService>();

// O OutputCache é usado apenas em rotas explicitamente marcadas — nunca como política
// global, para não servir respostas velhas logo após uma escrita.
builder.Services.AddOutputCache();
builder.Services.AddResponseCompression();

// ---------- Rate limiting ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Muitas requisições. Aguarde alguns segundos e tente novamente.", ct);
    };

    // política geral da API: 100 req / 10s por IP
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // login/cadastro: mais restrito contra força bruta (10 req / min por IP)
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // limite global de segurança
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromSeconds(10)
            }));
});

// ---------- CORS ----------
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---------- Health checks ----------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TriagemDbContext>("sqlserver");

// aceita X-Forwarded-For do load balancer (nginx)
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseResponseCompression();
app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();

app.MapOpenApi();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    servico = "Triar API",
    status = "online",
    instancia = Environment.MachineName,
    docs = "/openapi/v1.json"
}));

// ---------- Migração/seed com retry (aguarda o SQL Server subir) ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TriagemDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var tentativa = 1; ; tentativa++)
    {
        try
        {
            await DbSeeder.SeedAsync(db);
            logger.LogInformation("Banco de dados pronto.");
            break;
        }
        catch (Exception ex) when (tentativa < 10)
        {
            logger.LogWarning("Banco indisponível (tentativa {Tentativa}/10): {Erro}. Aguardando 5s...",
                tentativa, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

app.Run();
