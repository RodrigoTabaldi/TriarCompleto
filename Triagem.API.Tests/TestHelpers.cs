using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Triagem.API.Data;
using Triagem.API.Services;

namespace Triagem.API.Tests;

internal static class TestHelpers
{
    public static FieldEncryptionService NovoEncryptor() =>
        new(new DataProtectionOptions { Key = "chave-de-teste-com-mais-de-32-caracteres-para-aes" });

    /// <summary>Novo TriagemDbContext isolado (InMemory), um banco distinto por teste.</summary>
    public static TriagemDbContext NovoDbContext()
    {
        var options = new DbContextOptionsBuilder<TriagemDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // O provedor InMemory não suporta transações reais; o código de produção
            // as usa (SQL Server) para consistência entre TriagemModelo e a
            // preferência de home. Aqui só silenciamos o aviso — o comportamento
            // sob teste é o mesmo, sem a garantia transacional real do SQL Server.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TriagemDbContext(options);
    }

    public static CacheService NovoCacheService() =>
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<CacheService>.Instance);

    public static TriagemService NovoService(TriagemDbContext db) =>
        new(db, NovoCacheService(), NovoEncryptor(), NullLogger<TriagemService>.Instance);
}
