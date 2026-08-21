using Triagem.API.Services;

namespace Triagem.API.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_DepoisVerify_ComSenhaCorreta_RetornaTrue()
    {
        var hash = PasswordHasher.Hash("SenhaForte123");

        Assert.True(PasswordHasher.Verify("SenhaForte123", hash));
    }

    [Fact]
    public void Verify_ComSenhaErrada_RetornaFalse()
    {
        var hash = PasswordHasher.Hash("SenhaForte123");

        Assert.False(PasswordHasher.Verify("SenhaErrada", hash));
    }

    [Fact]
    public void Hash_MesmaSenhaDuasVezes_GeraHashesDiferentes()
    {
        // Salt aleatório: hashes da mesma senha nunca devem ser iguais.
        var hash1 = PasswordHasher.Hash("SenhaForte123");
        var hash2 = PasswordHasher.Hash("SenhaForte123");

        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordHasher.Verify("SenhaForte123", hash1));
        Assert.True(PasswordHasher.Verify("SenhaForte123", hash2));
    }

    [Fact]
    public void Verify_ComHashArmazenadoInvalido_RetornaFalseSemLancar()
    {
        Assert.False(PasswordHasher.Verify("qualquer", "formato-invalido-sem-pontos"));
    }

    [Fact]
    public void Verify_ComIteracoesNaoNumericas_RetornaFalseSemLancar()
    {
        Assert.False(PasswordHasher.Verify("qualquer", "abc.c2FsdA==.aGFzaA=="));
    }

    [Fact]
    public void Verify_ComSaltOuHashNaoBase64_RetornaFalseSemLancar()
    {
        Assert.False(PasswordHasher.Verify("qualquer", "100000.não-é-base64!.também-não!"));
    }

    [Fact]
    public void Verify_ComIteracoesZeroOuNegativas_RetornaFalseSemLancar()
    {
        Assert.False(PasswordHasher.Verify("qualquer", "0.c2FsdA==.aGFzaA=="));
        Assert.False(PasswordHasher.Verify("qualquer", "-5.c2FsdA==.aGFzaA=="));
    }
}
