using Triagem.API.Controllers;
using Triagem.API.Models;
using Triagem.API.Services;

namespace Triagem.API.Tests;

/// <summary>
/// Verifica estruturalmente a correção do vazamento de temporização em Login (ver
/// AuthController.cs): em vez de medir tempo de resposta (frágil e não-determinístico
/// em CI), estes testes confirmam que o hash usado na comparação nunca é nulo/vazio e
/// tem o mesmo formato de PasswordHasher — ou seja, PasswordHasher.Verify sempre roda
/// um PBKDF2 completo, mesmo quando o usuário não existe.
/// </summary>
public class AuthControllerHashTests
{
    [Fact]
    public void ResolverHashParaComparacao_UsuarioExistente_UsaSenhaHashDoUsuario()
    {
        var usuario = new Usuario { Id = 1, Nome = "Ana", Email = "ana@teste.com", SenhaHash = PasswordHasher.Hash("senha1234") };

        var hash = AuthController.ResolverHashParaComparacao(usuario);

        Assert.Equal(usuario.SenhaHash, hash);
    }

    [Fact]
    public void ResolverHashParaComparacao_UsuarioInexistente_RetornaHashDummyBemFormado()
    {
        var hash = AuthController.ResolverHashParaComparacao(null);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        // Mesmo formato de PasswordHasher.Hash: "{iterações}.{salt}.{hash}" — garante
        // que PasswordHasher.Verify(senha, hash) executa o PBKDF2 completo em vez de
        // curto-circuitar, o que é o que efetivamente fecha o vazamento de temporização.
        Assert.Equal(3, hash.Split('.').Length);
        Assert.True(PasswordHasher.Verify("qualquer-senha-nao-deve-bater", hash) == false);
    }

    [Fact]
    public void ResolverHashParaComparacao_ChamadasRepetidasParaUsuarioInexistente_UsamMesmoHashDummy()
    {
        // O hash dummy é fixo por processo (não gerado a cada chamada) — senão, cada
        // Login com email inexistente pagaria o custo de RandomNumberGenerator +
        // PBKDF2 duas vezes (uma para criar o dummy, outra para comparar).
        var primeiro = AuthController.ResolverHashParaComparacao(null);
        var segundo = AuthController.ResolverHashParaComparacao(null);

        Assert.Equal(primeiro, segundo);
    }
}
