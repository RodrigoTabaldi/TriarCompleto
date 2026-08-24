using Triagem.API.Data;
using Triagem.API.Dtos;
using Triagem.API.Models;
using Triagem.API.Services;

namespace Triagem.API.Tests;

public class TriagemServiceTests
{
    private static async Task<(TriagemDbContext Db, TriagemService Service, int UsuarioA, int UsuarioB)> NovoCenarioComDoisUsuariosAsync()
    {
        var db = TestHelpers.NovoDbContext();
        var service = TestHelpers.NovoService(db);

        var a = new Usuario { Nome = "Usuario A", Email = "a@teste.com", SenhaHash = "x" };
        var b = new Usuario { Nome = "Usuario B", Email = "b@teste.com", SenhaHash = "x" };
        db.Usuarios.AddRange(a, b);
        await db.SaveChangesAsync();

        return (db, service, a.Id, b.Id);
    }

    private static CriarTriagemRequest RequestValido(string titulo = "Triagem de Teste") => new(
        Titulo: titulo,
        PublicoAlvo: "Todas as idades",
        Descricao: "descricao",
        Icone: "🩺",
        Imagem: null,
        Perguntas: [new PerguntaInput("Pergunta 1?", 2), new PerguntaInput("Pergunta 2?", 3)],
        Faixas:
        [
            new FaixaInput("Baixo risco", "ok", 0, 2, null),
            new FaixaInput("Alto risco", "procure ajuda", 3, 5, null)
        ]);

    // ---------------- Regressão do IDOR (GET /api/triagens/{id}) ----------------

    [Fact]
    public async Task ObterDetalheAsync_TriagemPrivadaDeOutroUsuario_RetornaNull()
    {
        var (_, service, usuarioA, usuarioB) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, erro) = await service.CriarAsync(usuarioA, RequestValido());
        Assert.Null(erro);

        var detalheParaB = await service.ObterDetalheAsync(usuarioB, criada!.Id);

        Assert.Null(detalheParaB);
    }

    [Fact]
    public async Task ObterDetalheAsync_TriagemPrivadaDoProprioDono_RetornaDetalhe()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        var detalhe = await service.ObterDetalheAsync(usuarioA, criada!.Id);

        Assert.NotNull(detalhe);
        Assert.Equal(criada.Id, detalhe!.Id);
    }

    [Fact]
    public async Task ObterDetalheAsync_TriagemPadraoDoSistema_VisivelParaQualquerUsuario()
    {
        var db = TestHelpers.NovoDbContext();
        var service = TestHelpers.NovoService(db);
        var usuario = new Usuario { Nome = "Ana", Email = "ana@teste.com", SenhaHash = "x" };
        db.Usuarios.Add(usuario);

        var padrao = new TriagemModelo
        {
            Titulo = "Triagem Padrão",
            PublicoAlvo = "Todas as idades",
            CriadorUsuarioId = null,
            Ativa = true,
            Perguntas = [new Pergunta { Texto = "P1", Peso = 1, Ordem = 1 }],
            Faixas =
            [
                new FaixaResultado { Titulo = "Baixo", PontuacaoMin = 0, PontuacaoMax = 0, Ordem = 1 },
                new FaixaResultado { Titulo = "Alto", PontuacaoMin = 1, PontuacaoMax = 1, Ordem = 2 }
            ]
        };
        db.TriagemModelos.Add(padrao);
        await db.SaveChangesAsync();

        var detalhe = await service.ObterDetalheAsync(usuario.Id, padrao.Id);

        Assert.NotNull(detalhe);
        Assert.True(detalhe!.Padrao);
    }

    // ---------------- Autorização em escrita (editar/excluir) ----------------

    [Fact]
    public async Task AtualizarAsync_PorUsuarioQueNaoECriador_RetornaErro()
    {
        var (_, service, usuarioA, usuarioB) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        var (ok, erro) = await service.AtualizarAsync(usuarioB, criada!.Id, RequestValido("Hackeado"));

        Assert.False(ok);
        Assert.Equal("Apenas o criador pode editar esta triagem.", erro);
    }

    [Fact]
    public async Task DesativarAsync_PorUsuarioQueNaoECriador_RetornaErro()
    {
        var (_, service, usuarioA, usuarioB) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        var (ok, erro) = await service.DesativarAsync(usuarioB, criada!.Id);

        Assert.False(ok);
        Assert.Equal("Apenas o criador pode excluir esta triagem.", erro);
    }

    [Fact]
    public async Task DesativarAsync_PeloProprioCriador_Funciona()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        var (ok, erro) = await service.DesativarAsync(usuarioA, criada!.Id);

        Assert.True(ok);
        Assert.Null(erro);
        Assert.Null(await service.ObterDetalheAsync(usuarioA, criada.Id));
    }

    // ---------------- Validação de modelo ----------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CriarAsync_SemTitulo_RetornaErro(string titulo)
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();

        var (detalhe, erro) = await service.CriarAsync(usuarioA, RequestValido(titulo));

        Assert.Null(detalhe);
        Assert.NotNull(erro);
    }

    [Fact]
    public async Task CriarAsync_SemPerguntas_RetornaErro()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var req = RequestValido() with { Perguntas = [] };

        var (detalhe, erro) = await service.CriarAsync(usuarioA, req);

        Assert.Null(detalhe);
        Assert.Equal("Adicione pelo menos uma pergunta.", erro);
    }

    [Fact]
    public async Task CriarAsync_ComPesoForaDoIntervalo_RetornaErro()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var req = RequestValido() with { Perguntas = [new PerguntaInput("P?", 0)] };

        var (detalhe, erro) = await service.CriarAsync(usuarioA, req);

        Assert.Null(detalhe);
        Assert.Equal("O peso de cada pergunta deve estar entre 1 e 100.", erro);
    }

    [Fact]
    public async Task CriarAsync_ComFaixasSobrepostas_RetornaErro()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var req = RequestValido() with
        {
            Faixas = [new FaixaInput("A", "r", 0, 3, null), new FaixaInput("B", "r", 2, 5, null)]
        };

        var (detalhe, erro) = await service.CriarAsync(usuarioA, req);

        Assert.Null(detalhe);
        Assert.Equal("As faixas de resultado não podem se sobrepor.", erro);
    }

    [Fact]
    public async Task CriarAsync_ComFaixasQueNaoComecamEmZero_RetornaErro()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var req = RequestValido() with
        {
            Faixas = [new FaixaInput("A", "r", 1, 2, null), new FaixaInput("B", "r", 3, 5, null)]
        };

        var (detalhe, erro) = await service.CriarAsync(usuarioA, req);

        Assert.Null(detalhe);
        Assert.Equal("A primeira faixa deve começar em 0.", erro);
    }

    [Fact]
    public async Task CriarAsync_ComFaixasQueNaoCobremAPontuacaoMaxima_RetornaErro()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        // pontuação máxima = 2 + 3 = 5, mas a última faixa só vai até 3
        var req = RequestValido() with
        {
            Faixas = [new FaixaInput("A", "r", 0, 1, null), new FaixaInput("B", "r", 2, 3, null)]
        };

        var (detalhe, erro) = await service.CriarAsync(usuarioA, req);

        Assert.Null(detalhe);
        Assert.StartsWith("A última faixa deve cobrir até a pontuação máxima", erro);
    }

    // ---------------- Cálculo de pontuação/classificação (ResponderAsync) ----------------

    [Fact]
    public async Task ResponderAsync_SomaPesoApenasDasRespostasSim()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());
        var perguntas = criada!.Perguntas;

        var respostas = new ResponderTriagemRequest(
            NomePaciente: "Paciente Teste", Idade: 30, Sexo: "F",
            Respostas:
            [
                new RespostaInput(perguntas[0].Id, true),   // peso 2
                new RespostaInput(perguntas[1].Id, false)   // peso 3, não conta
            ]);

        var (resultado, erro) = await service.ResponderAsync(usuarioA, criada.Id, respostas);

        Assert.Null(erro);
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado!.Pontuacao);
        Assert.Equal(5, resultado.PontuacaoMaxima);
        Assert.Equal("Baixo risco", resultado.Classificacao);
    }

    [Fact]
    public async Task ResponderAsync_ComTodasAsRespostasSim_ClassificaNaFaixaMaisAlta()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());
        var perguntas = criada!.Perguntas;

        var respostas = new ResponderTriagemRequest(
            NomePaciente: "Paciente Teste", Idade: 30, Sexo: "F",
            Respostas: perguntas.Select(p => new RespostaInput(p.Id, true)).ToList());

        var (resultado, _) = await service.ResponderAsync(usuarioA, criada.Id, respostas);

        Assert.Equal(5, resultado!.Pontuacao);
        Assert.Equal("Alto risco", resultado.Classificacao);
    }

    [Fact]
    public async Task ResponderAsync_ComPerguntaQueNaoPertenceATriagem_RetornaErro()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        var respostas = new ResponderTriagemRequest(
            NomePaciente: "Paciente Teste", Idade: 30, Sexo: "F",
            Respostas: [new RespostaInput(PerguntaId: 999999, Valor: true)]);

        var (resultado, erro) = await service.ResponderAsync(usuarioA, criada!.Id, respostas);

        Assert.Null(resultado);
        Assert.Contains("não pertence a esta triagem", erro);
    }

    [Fact]
    public async Task ResponderAsync_ComIdadeInvalida_RetornaErro()
    {
        var (_, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        var respostas = new ResponderTriagemRequest(
            NomePaciente: "Paciente Teste", Idade: 200, Sexo: "F", Respostas: []);

        var (resultado, erro) = await service.ResponderAsync(usuarioA, criada!.Id, respostas);

        Assert.Null(resultado);
        Assert.Equal("Idade inválida.", erro);
    }

    // ---------------- Histórico paginado ----------------

    [Fact]
    public async Task HistoricoAsync_RespeitaLimiteMaximoDeTamanhoDePagina()
    {
        var (db, service, usuarioA, _) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        for (var i = 0; i < 5; i++)
        {
            db.TriagemResultados.Add(new TriagemResultado
            {
                TriagemModeloId = criada!.Id,
                UsuarioId = usuarioA,
                NomePaciente = $"Paciente {i}",
                Idade = 20,
                Sexo = "F",
                Pontuacao = 0,
                PontuacaoMaxima = 5,
                Classificacao = "Baixo risco",
                Data = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        // pede um tamanho de página maior que o teto (200) mas só existem 5 registros
        var pagina1 = await service.HistoricoAsync(usuarioA, criada!.Id, pagina: 1, tamanhoPagina: 3);
        var pagina2 = await service.HistoricoAsync(usuarioA, criada.Id, pagina: 2, tamanhoPagina: 3);

        Assert.Equal(3, pagina1.Count);
        Assert.Equal(2, pagina2.Count);
        // mais recente primeiro: "Paciente 0" tem a data mais recente
        Assert.Equal("Paciente 0", pagina1[0].Nome);
    }

    [Fact]
    public async Task HistoricoAsync_SoRetornaResultadosDoUsuarioDono()
    {
        var (db, service, usuarioA, usuarioB) = await NovoCenarioComDoisUsuariosAsync();
        var (criada, _) = await service.CriarAsync(usuarioA, RequestValido());

        db.TriagemResultados.Add(new TriagemResultado
        {
            TriagemModeloId = criada!.Id, UsuarioId = usuarioA, NomePaciente = "Paciente de A",
            Idade = 20, Sexo = "F", Pontuacao = 0, PontuacaoMaxima = 5, Classificacao = "Baixo risco"
        });
        await db.SaveChangesAsync();

        var historicoDeB = await service.HistoricoAsync(usuarioB);

        Assert.Empty(historicoDeB);
    }
}
