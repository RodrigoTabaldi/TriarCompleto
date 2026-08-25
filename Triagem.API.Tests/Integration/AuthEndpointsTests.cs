using System.Net;
using System.Net.Http.Json;
using Triagem.API.Dtos;

namespace Triagem.API.Tests.Integration;

/// <summary>
/// Testes de integração HTTP dos endpoints de autenticação: exercitam o pipeline
/// completo (roteamento, model binding, [Authorize]/[AllowAnonymous], rate limiting)
/// que os testes de unidade de TriagemServiceTests não alcançam por chamarem os
/// serviços diretamente.
/// </summary>
public class AuthEndpointsTests
{
    [Fact]
    public async Task Register_ComDadosValidos_RetornaTokenEUsuario()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();

        using var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { nome = "Ana Teste", email = "ana@teste.com", senha = "senha1234" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.Equal("ana@teste.com", auth.Email);
    }

    [Fact]
    public async Task Register_ComEmailJaCadastrado_RetornaConflict()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();
        var payload = new { nome = "Ana Teste", email = "duplicado@teste.com", senha = "senha1234" };

        using var primeira = await client.PostAsJsonAsync("/api/auth/register", payload);
        using var segunda = await client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Register_ComSenhaCurta_RetornaBadRequest()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();

        using var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { nome = "Ana Teste", email = "ana2@teste.com", senha = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_RetornaToken()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new { nome = "Bia Teste", email = "bia@teste.com", senha = "senha1234" });

        using var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "bia@teste.com", senha = "senha1234" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
    }

    [Fact]
    public async Task Login_ComSenhaErrada_RetornaUnauthorized()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new { nome = "Carla Teste", email = "carla@teste.com", senha = "senha1234" });

        using var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "carla@teste.com", senha = "senha-errada" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>
    /// Guarda de regressão funcional para o vazamento de temporização corrigido em
    /// AuthController: um email nunca cadastrado deve se comportar exatamente como um
    /// email cadastrado com senha errada — mesmo status, mesma mensagem genérica.
    /// (A prova de que o custo de hash é pago nos dois casos está no teste de unidade
    /// AuthControllerHashTests, que não depende de medir tempo.)
    /// </summary>
    [Fact]
    public async Task Login_ComEmailInexistente_RetornaUnauthorizedComMensagemGenerica()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();

        using var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nao-existe@teste.com", senha = "qualquer-coisa" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var corpo = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Email ou senha inválidos", corpo);
    }

    [Fact]
    public async Task Triagens_SemToken_RetornaUnauthorized()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();

        using var resp = await client.GetAsync("/api/triagens");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Triagens_ComTokenValido_RetornaOk()
    {
        using var factory = new TriarWebApplicationFactory();
        using var client = factory.CreateClient();
        using var registro = await client.PostAsJsonAsync("/api/auth/register",
            new { nome = "Duda Teste", email = "duda@teste.com", senha = "senha1234" });
        var auth = await registro.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);
        using var resp = await client.GetAsync("/api/triagens");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
