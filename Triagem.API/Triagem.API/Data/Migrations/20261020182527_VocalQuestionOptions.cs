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
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Perguntas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpcoesJson",
                table: "Perguntas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "SelecaoUnica",
                table: "Perguntas",
                type: "bit",
                nullable: false,
                defaultValue: false);
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
