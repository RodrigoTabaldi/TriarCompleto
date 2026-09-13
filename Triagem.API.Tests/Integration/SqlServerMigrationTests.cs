using Microsoft.EntityFrameworkCore;
using Triagem.API.Data;
using Triagem.API.Services;
using Triagem.Core.Domain;

namespace Triagem.API.Tests.Integration;

public class SqlServerMigrationTests
{
    [Fact]
    public async Task MigrationsESeed_SaoIdempotentesNoSqlServerReal()
    {
        var connectionString = Environment.GetEnvironmentVariable("TRIAR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.False(string.Equals(Environment.GetEnvironmentVariable("CI"), "true",
                StringComparison.OrdinalIgnoreCase),
                "TRIAR_TEST_SQLSERVER precisa estar configurada na CI.");
            return;
        }

        var options = new DbContextOptionsBuilder<TriagemDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var encryptor = new FieldEncryptionService(new DataProtectionOptions
        {
            Key = "chave-descartavel-do-teste-sql-server-com-mais-de-32-caracteres",
            KeyId = "ci"
        });

        await using var db = new TriagemDbContext(options);
        try
        {
            await DbSeeder.SeedAsync(db, encryptor);
            await DbSeeder.SeedAsync(db, encryptor);

            var modelos = await db.TriagemModelos
                .AsNoTracking()
                .Include(t => t.Perguntas)
                .Where(t => t.CriadorUsuarioId == null)
                .ToListAsync();

            Assert.Equal(DefaultTriageCatalog.Items.Count, modelos.Count);
            foreach (var item in DefaultTriageCatalog.Items)
            {
                var modelo = Assert.Single(modelos, m => m.Titulo == item.Title);
                Assert.Equal(item.Questions.Count, modelo.Perguntas.Count);
                Assert.DoesNotContain(modelo.Perguntas,
                    p => p.Texto.Contains("ciclo menstrual", StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
