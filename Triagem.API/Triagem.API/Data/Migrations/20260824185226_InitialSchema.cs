using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triagem.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriagemModelos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PublicoAlvo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Icone = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Imagem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadorUsuarioId = table.Column<int>(type: "int", nullable: true),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriagemModelos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriagemModelos_Usuarios_CriadorUsuarioId",
                        column: x => x.CriadorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FaixasResultado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TriagemModeloId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Recomendacao = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    PontuacaoMin = table.Column<int>(type: "int", nullable: false),
                    PontuacaoMax = table.Column<int>(type: "int", nullable: false),
                    Cor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaixasResultado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaixasResultado_TriagemModelos_TriagemModeloId",
                        column: x => x.TriagemModeloId,
                        principalTable: "TriagemModelos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Perguntas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TriagemModeloId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Peso = table.Column<int>(type: "int", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perguntas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Perguntas_TriagemModelos_TriagemModeloId",
                        column: x => x.TriagemModeloId,
                        principalTable: "TriagemModelos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TriagemResultados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TriagemModeloId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    NomePaciente = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Idade = table.Column<int>(type: "int", nullable: false),
                    Sexo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Pontuacao = table.Column<int>(type: "int", nullable: false),
                    PontuacaoMaxima = table.Column<int>(type: "int", nullable: false),
                    Classificacao = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Recomendacao = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Cor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriagemResultados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriagemResultados_TriagemModelos_TriagemModeloId",
                        column: x => x.TriagemModeloId,
                        principalTable: "TriagemModelos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TriagemResultados_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioTriagensHome",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    TriagemModeloId = table.Column<int>(type: "int", nullable: false),
                    Visivel = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioTriagensHome", x => new { x.UsuarioId, x.TriagemModeloId });
                    table.ForeignKey(
                        name: "FK_UsuarioTriagensHome_TriagemModelos_TriagemModeloId",
                        column: x => x.TriagemModeloId,
                        principalTable: "TriagemModelos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioTriagensHome_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RespostasDadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TriagemResultadoId = table.Column<int>(type: "int", nullable: false),
                    PerguntaId = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostasDadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasDadas_TriagemResultados_TriagemResultadoId",
                        column: x => x.TriagemResultadoId,
                        principalTable: "TriagemResultados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaixasResultado_TriagemModeloId",
                table: "FaixasResultado",
                column: "TriagemModeloId");

            migrationBuilder.CreateIndex(
                name: "IX_Perguntas_TriagemModeloId",
                table: "Perguntas",
                column: "TriagemModeloId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasDadas_TriagemResultadoId",
                table: "RespostasDadas",
                column: "TriagemResultadoId");

            migrationBuilder.CreateIndex(
                name: "IX_TriagemModelos_CriadorUsuarioId",
                table: "TriagemModelos",
                column: "CriadorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_TriagemResultados_TriagemModeloId",
                table: "TriagemResultados",
                column: "TriagemModeloId");

            migrationBuilder.CreateIndex(
                name: "IX_TriagemResultados_UsuarioId_TriagemModeloId",
                table: "TriagemResultados",
                columns: new[] { "UsuarioId", "TriagemModeloId" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioTriagensHome_TriagemModeloId",
                table: "UsuarioTriagensHome",
                column: "TriagemModeloId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaixasResultado");

            migrationBuilder.DropTable(
                name: "Perguntas");

            migrationBuilder.DropTable(
                name: "RespostasDadas");

            migrationBuilder.DropTable(
                name: "UsuarioTriagensHome");

            migrationBuilder.DropTable(
                name: "TriagemResultados");

            migrationBuilder.DropTable(
                name: "TriagemModelos");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
