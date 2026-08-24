using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triagem.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TriagemResultados_UsuarioId_TriagemModeloId",
                table: "TriagemResultados");

            migrationBuilder.CreateIndex(
                name: "IX_TriagemResultados_UsuarioId_TriagemModeloId_Data",
                table: "TriagemResultados",
                columns: new[] { "UsuarioId", "TriagemModeloId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TriagemResultados_UsuarioId_TriagemModeloId_Data",
                table: "TriagemResultados");

            migrationBuilder.CreateIndex(
                name: "IX_TriagemResultados_UsuarioId_TriagemModeloId",
                table: "TriagemResultados",
                columns: new[] { "UsuarioId", "TriagemModeloId" });
        }
    }
}
