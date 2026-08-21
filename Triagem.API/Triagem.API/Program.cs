using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Triagem.API.Data;
using Triagem.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Porta (PaaS: Render, Railway, Fly...) ----------
// Plataformas de container injetam a porta a escutar em PORT e roteiam o tráfego
// público para ela. Sem honrar essa variável, o container sobe escutando outra porta
// e a plataforma o considera "não saudável", derrubando o deploy.
var portaPaas = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portaPaas))
    builder.WebHost.UseUrls($"http://0.0.0.0:{portaPaas}");

// ---------- Banco de dados (SQL Server / Azure SQL) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

// ---------- Criptografia de campos sensíveis (nome do paciente em repouso) ----------
// A chave vem de configuração/ambiente (DataProtection:Key) — nunca versionada.
// "ChavesAnteriores" (opcional) mantém chaves de uma rotação passada disponíveis só
// para leitura, para que trocar a chave não torne ilegíveis os registros já gravados.
var dataProtection = builder.Configuration.GetSection("DataProtection").Get<DataProtectionOptions>() ?? new DataProtectionOptions();
builder.Services.AddSingleton(sp =>
    new FieldEncryptionService(dataProtection, sp.GetRequiredService<ILogger<FieldEncryptionService>>()));

builder.Services.AddDbContext<TriagemDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        // No Azure SQL serverless o banco pausa quando ocioso e leva ~1 min para
        // retomar; a primeira conexão depois disso falha com o erro 40613, que o
        // EF Core já classifica como transitório. Sem esse retry, o primeiro acesso
        // do dia devolveria erro ao usuário em vez de apenas demorar um pouco.
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
        sql.CommandTimeout(60);
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

    // Conexão direta (fora do IDistributedCache) usada só pelo DistributedRateLimiter,
    // que precisa de INCR/EXPIRE atômicos — operação que a abstração IDistributedCache
    // não expõe.
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        _ => ConnectionMultiplexer.Connect(redisConnection));
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSingleton<CacheService>();

// ---------- Rate limiter distribuído (política "auth") ----------
// Sem IConnectionMultiplexer registrado (sem Redis configurado), DistributedRateLimiter
// recebe null e simplesmente não atua — o limitador em memória do ASP.NET Core abaixo
// já é exato nesse cenário (processo único).
builder.Services.AddSingleton(sp => new DistributedRateLimiter(
    sp.GetService<IConnectionMultiplexer>(),
    sp.GetRequiredService<ILogger<DistributedRateLimiter>>()));

// ---------- Autenticação (JWT) ----------
// A chave vem de configuração/ambiente (Jwt:Key) — nunca versionada.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key ausente ou fraca (mínimo 32 caracteres). Configure via variável de ambiente Jwt__Key.");

builder.Services.AddSingleton(jwt);
builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

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
// Origens liberadas vêm de "Cors:AllowedOrigins". Sem configuração explícita
// (ex.: apps MAUI nativos, que não enviam Origin), nenhuma origem web é liberada.
var origensPermitidas = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
    {
        if (origensPermitidas.Length > 0)
            p.WithOrigins(origensPermitidas).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }));

// ---------- Health checks ----------
// A checagem do banco fica marcada com a tag "db" para poder ser EXCLUÍDA do
// endpoint de liveness. Isso é essencial no Azure SQL serverless: qualquer conexão
// ao banco conta como sessão aberta e impede o auto-pause, então um monitor batendo
// de minuto em minuto num health check que consulta o banco manteria o banco ligado
// 24/7 e consumiria toda a cota mensal em poucos dias.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TriagemDbContext>("banco", tags: ["db"]);

// aceita X-Forwarded-For apenas de proxies confiáveis (o nginx/rede docker),
// para que o rate limit por IP não seja burlável via spoof do header.
// "ForwardedHeaders:KnownProxies" (IPs) e "ForwardedHeaders:KnownNetworks" (CIDR "ip/prefixo").
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;

    // Quantos proxies existem na frente da API. Docker + nginx local = 2;
    // um PaaS como o Render põe apenas o proxy dele = 1.
    o.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 2);

    // Em PaaS o IP interno do proxy não é conhecido de antemão e pode mudar a cada
    // deploy, então não dá para listá-lo. Esvaziar as listas faz o ASP.NET aceitar o
    // header de qualquer peer — o que só é seguro porque o proxy da plataforma
    // ACRESCENTA o IP real do cliente ao fim do X-Forwarded-For, e com ForwardLimit=1
    // é justamente esse último valor que o ASP.NET usa. Um cliente que forje o header
    // consegue no máximo poluir as entradas à esquerda, que são descartadas.
    if (builder.Configuration.GetValue("ForwardedHeaders:TrustPlatformProxy", false))
    {
        o.KnownProxies.Clear();
        o.KnownIPNetworks.Clear();
        o.ForwardLimit = 1;
    }
    else
    {
        foreach (var ip in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            if (IPAddress.TryParse(ip, out var addr)) o.KnownProxies.Add(addr);

        foreach (var cidr in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            if (IPNetwork.TryParse(cidr, out var rede))
                o.KnownIPNetworks.Add(rede);
    }
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseForwardedHeaders();

// HSTS instrui o navegador a nunca mais acessar este host em HTTP puro. Fora de
// Development apenas: em dev o host é http://localhost e o header atrapalharia.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Cabeçalhos de defesa em profundidade. A API só devolve JSON, então negar sniffing
// de conteúdo e enquadramento em iframe custa nada e fecha classes inteiras de abuso
// caso alguma resposta acabe sendo renderizada por um navegador.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseResponseCompression();
app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();

// O documento OpenAPI descreve todas as rotas, parâmetros e formatos da API. É uma
// planta baixa para quem quiser sondá-la, e não tem serventia em produção — o app
// MAUI não o consome. Fica restrito a Development.
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();

// Liveness: responde sem tocar no banco. É este o endpoint que o Render deve usar
// como health check e o único seguro para manter aquecido — ver o comentário em
// AddHealthChecks sobre o auto-pause do Azure SQL.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: verifica o banco de verdade. Use sob demanda (diagnóstico, docker-compose),
// nunca em monitoramento periódico contra o Azure SQL serverless.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("db")
});

app.MapGet("/", () => Results.Ok(new
{
    servico = "Triar API",
    status = "online"
}));

// ---------- Criação/seed do banco, com retry (aguarda o banco subir) ----------
// Controlado por "Database:SeedOnStartup" (padrão: ligado).
//
// No docker-compose isso precisa rodar a cada boot, porque o SQL Server sobe vazio.
// Num PaaS com Azure SQL serverless é o contrário: o banco já existe e persiste, e
// ligar a API é algo que acontece toda vez que o serviço acorda de uma dormida — se
// o seed rodasse aí, cada despertar da API acordaria o banco junto, gastando a cota
// mensal mesmo quando ninguém abriu o app. Depois do primeiro deploy bem-sucedido,
// defina Database__SeedOnStartup=false no Render.
if (app.Configuration.GetValue("Database:SeedOnStartup", true))
{
    using var scope = app.Services.CreateScope();
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
