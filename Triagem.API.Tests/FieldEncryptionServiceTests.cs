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
    public void Decrypt_ComOutraChaveNaoConfigurada_NaoLancaERetornaMarcadorDeFalha()
    {
        // Antes: uma chave errada (ex.: rotação sem registrar a anterior) lançava
        // AuthenticationTagMismatchException. Como Decrypt roda como value converter
        // dentro de uma projeção do EF, isso derrubava com HTTP 500 a página inteira
        // do histórico por causa de UM registro. Agora falha graciosamente.
        var encA = new FieldEncryptionService(new DataProtectionOptions { Key = "chave-de-teste-A-com-mais-de-32-caracteres" });
        var encB = new FieldEncryptionService(new DataProtectionOptions { Key = "chave-de-teste-B-completamente-diferente-32" });

        var cifrado = encA.Encrypt("dado sensível");
        var resultado = encB.Decrypt(cifrado);

        Assert.DoesNotContain("dado sensível", resultado);
        Assert.Contains("falha ao descriptografar", resultado);
    }

    [Fact]
    public void Decrypt_ComChaveAnteriorConfigurada_DescriptografaComSucesso()
    {
        // Rotação de chave sem perda de dados: a chave nova é usada para ENCRYPT, mas
        // DECRYPT ainda reconhece registros gravados com a chave anterior.
        var chaveAntiga = "chave-antiga-antes-da-rotacao-com-32-chars";
        var encAntigo = new FieldEncryptionService(new DataProtectionOptions { Key = chaveAntiga });
        var cifradoComChaveAntiga = encAntigo.Encrypt("Maria da Silva");

        var encRotacionado = new FieldEncryptionService(new DataProtectionOptions
        {
            Key = "chave-nova-depois-da-rotacao-tambem-com-32-c",
            ChavesAnteriores = [chaveAntiga]
        });

        Assert.Equal("Maria da Silva", encRotacionado.Decrypt(cifradoComChaveAntiga));
    }

    [Fact]
    public void Decrypt_DadoLegadoEmTextoPlano_RetornaComoVeio()
    {
        // Registros gravados antes de esta criptografia existir não têm o formato
        // esperado (nonce+tag+cifra em base64) — Decrypt deve devolvê-los como estão
        // em vez de lançar, para não quebrar o histórico de contas antigas.
        var enc = TestHelpers.NovoEncryptor();

        Assert.Equal("Maria da Silva", enc.Decrypt("Maria da Silva"));
    }

    [Fact]
    public void Decrypt_NuloOuVazio_RetornaVazio()
    {
        var enc = TestHelpers.NovoEncryptor();

        Assert.Equal("", enc.Decrypt(null));
        Assert.Equal("", enc.Decrypt(""));
    }
}
