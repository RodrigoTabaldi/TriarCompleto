using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Triagem.API.Services;

namespace Triagem.API.Tests;

public class CacheServiceTests
{
    [Fact]
    public async Task BumpVersionAsync_PublicaNovaGeracaoEmCadaInvalidacao()
    {
        var distribuido = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new CacheService(distribuido, NullLogger<CacheService>.Instance);

        await cache.BumpVersionAsync();
        var primeira = await cache.GetVersionAsync();
        await cache.BumpVersionAsync();
        var segunda = await cache.GetVersionAsync();

        Assert.NotEqual("0", primeira);
        Assert.NotEqual(primeira, segunda);
    }
}
