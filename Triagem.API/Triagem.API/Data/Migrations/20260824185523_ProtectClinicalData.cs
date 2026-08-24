using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triagem.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProtectClinicalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DadosProtegidos",
                table: "TriagemResultados",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValorProtegido",
                table: "RespostasDadas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DadosProtegidos",
                table: "TriagemResultados");

            migrationBuilder.DropColumn(
                name: "ValorProtegido",
                table: "RespostasDadas");
        }
    }
}
