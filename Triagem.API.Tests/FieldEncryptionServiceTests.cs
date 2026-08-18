using Triagem.API.Services;

namespace Triagem.API.Tests;

public class FieldEncryptionServiceTests
{
    [Fact]
    public void Encrypt_DepoisDecrypt_RetornaTextoOriginal()
    {
        var enc = TestHelpers.NovoEncryptor();

        var cifrado = enc.Encrypt("Maria da Silva");

        Assert.Equal("Maria da Silva", enc.Decrypt(cifrado));
    }

    [Fact]
    public void Encrypt_NaoRetornaTextoPlano()
    {
        var enc = TestHelpers.NovoEncryptor();

        var cifrado = enc.Encrypt("Maria da Silva");

        Assert.DoesNotContain("Maria", cifrado);
    }

    [Fact]
    public void Encrypt_MesmoTextoDuasVezes_GeraCifrasDiferentes()
    {
        // Nonce aleatório por chamada: mesma entrada não pode produzir a mesma saída
        // (senão dá para comparar registros cifrados e inferir nomes repetidos).
        var enc = TestHelpers.NovoEncryptor();

        var c1 = enc.Encrypt("Maria da Silva");
        var c2 = enc.Encrypt("Maria da Silva");

        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void Construtor_ComChaveCurta_Lanca()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new FieldEncryptionService(new DataProtectionOptions { Key = "chave-curta" }));
    }

    [Fact]
    public void Decrypt_ComOutraChave_Lanca()
    {
        var encA = new FieldEncryptionService(new DataProtectionOptions { Key = "chave-de-teste-A-com-mais-de-32-caracteres" });
        var encB = new FieldEncryptionService(new DataProtectionOptions { Key = "chave-de-teste-B-completamente-diferente-32" });

        var cifrado = encA.Encrypt("dado sensível");

        Assert.ThrowsAny<Exception>(() => encB.Decrypt(cifrado));
    }
}
