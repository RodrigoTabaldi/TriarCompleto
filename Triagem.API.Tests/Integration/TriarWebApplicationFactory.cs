using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Triagem.API.Data;

namespace Triagem.API.Tests.Integration;

/// <summary>
/// Sobe a API real (Program.cs completo: autenticação JWT, rate limiting, cabeçalhos
/// de segurança, roteamento) para testes de integração HTTP — a camada que os testes
/// de unidade de TriagemServiceTests não alcançam, porque chamam os serviços direto.
/// O banco é trocado por EF InMemory (isolado por instância desta factory) e o seed
/// automático fica desligado: cada teste popula só os dados de que precisa.
/// </summary>
internal sealed class TriarWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _nomeBanco = $"triar-integration-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Nunca usada de verdade (o DbContext é substituído por InMemory
                // abaixo) — só precisa passar da checagem de "não vazia" em Program.cs.
                ["ConnectionStrings:DefaultConnection"] = "Server=(unused);Database=Unused;Trusted_Connection=True;",
                ["Jwt:Key"] = "chave-de-teste-de-integracao-com-mais-de-32-caracteres",
                ["DataProtection:Key"] = "chave-de-teste-de-integracao-para-cripto-32-chars",
                // O seed real usa sp_getapplock e ALTER TABLE via SQL puro — não
                // suportado pelo provedor InMemory. Cada teste semeia os próprios dados.
                ["Database:SeedOnStartup"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TriagemDbContext>>();

            // AddDbContext original (Program.cs) já registrou os serviços internos do
            // provedor SqlServer no container raiz; registrar o InMemory por cima dele
            // no MESMO container faz o EF Core rejeitar com "only a single database
            // provider can be registered". UseInternalServiceProvider isola o InMemory
            // no próprio provider, sem tocar no que o SqlServer já registrou.
            var servicosEfInMemory = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<TriagemDbContext>(options =>
            {
                options.UseInMemoryDatabase(_nomeBanco);
                options.UseInternalServiceProvider(servicosEfInMemory);
            });
        });
    }
}
