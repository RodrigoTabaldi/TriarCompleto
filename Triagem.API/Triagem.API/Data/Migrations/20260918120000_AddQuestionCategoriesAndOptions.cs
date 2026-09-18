using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Triagem.API.Data.Migrations;

[DbContext(typeof(TriagemDbContext))]
[Migration("20260918120000_AddQuestionCategoriesAndOptions")]
public sealed class AddQuestionCategoriesAndOptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Categoria",
            table: "Perguntas",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "OpcoesJson",
            table: "Perguntas",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Categoria", table: "Perguntas");
        migrationBuilder.DropColumn(name: "OpcoesJson", table: "Perguntas");
    }
}
