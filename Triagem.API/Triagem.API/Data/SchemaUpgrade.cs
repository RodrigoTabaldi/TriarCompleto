using Microsoft.EntityFrameworkCore;

namespace Triagem.API.Data;

// Upgrade aditivo para bancos existentes criados por EnsureCreated. Não remove dados.
public static class SchemaUpgrade
{
    public static async Task ApplyAsync(TriagemDbContext db)
    {
        (string Table, string Column, string Default)[] columns =
        [
            ("Perguntas", "Categoria", ""), ("Perguntas", "OpcoesJson", "[]"),
            ("TriagemResultados", "Escolaridade", ""), ("TriagemResultados", "DoencasPrevias", ""),
            ("RespostasDadas", "OpcoesSelecionadasJson", "[]")
        ];
        foreach (var (table, column, value) in columns)
        {
            if (db.Database.IsSqlite())
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"PRAGMA table_info(\"{table}\")";
                var found = false;
                await using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) found |= reader.GetString(1) == column;
                if (found) continue;
            }
            var guard = db.Database.IsNpgsql() ? "IF NOT EXISTS " : "";
            // Identificadores e valores vêm exclusivamente da lista fixa acima.
            var sql = $"ALTER TABLE \"{table}\" ADD COLUMN {guard}\"{column}\" TEXT NOT NULL DEFAULT '{value}'";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
