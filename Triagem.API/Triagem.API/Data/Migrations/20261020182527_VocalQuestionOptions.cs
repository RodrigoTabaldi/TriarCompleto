using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triagem.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class VocalQuestionOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Perguntas', 'Categoria') IS NULL
                    ALTER TABLE [Perguntas] ADD [Categoria] nvarchar(max) NOT NULL DEFAULT N'';

                IF COL_LENGTH('dbo.Perguntas', 'OpcoesJson') IS NULL
                    ALTER TABLE [Perguntas] ADD [OpcoesJson] nvarchar(max) NOT NULL DEFAULT N'[]';

                IF COL_LENGTH('dbo.Perguntas', 'SelecaoUnica') IS NULL
                    ALTER TABLE [Perguntas] ADD [SelecaoUnica] bit NOT NULL DEFAULT CAST(0 AS bit);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Perguntas");

            migrationBuilder.DropColumn(
                name: "OpcoesJson",
                table: "Perguntas");

            migrationBuilder.DropColumn(
                name: "SelecaoUnica",
                table: "Perguntas");
        }
    }
}
