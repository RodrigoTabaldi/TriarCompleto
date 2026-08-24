using System.Security.Cryptography;
using Triagem.Core.Security;

namespace Triagem.Core.Tests;

public class AesGcmEnvelopeTests
{
    [Fact]
    public void EncryptDecrypt_PreservaTextoUnicode()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var envelope = AesGcmEnvelope.Encrypt(key, "Paciente João — classificação alta");

        Assert.Equal("Paciente João — classificação alta", AesGcmEnvelope.Decrypt(key, envelope));
        Assert.DoesNotContain("Paciente", envelope);
    }

    [Fact]
    public void Decrypt_ComEnvelopeAdulterado_FalhaAutenticacao()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var bytes = Convert.FromBase64String(AesGcmEnvelope.Encrypt(key, "sensível"));
        bytes[^1] ^= 0x01;

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            AesGcmEnvelope.Decrypt(key, Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void ChaveComTamanhoInvalido_ERejeitada()
    {
        Assert.Throws<ArgumentException>(() => AesGcmEnvelope.Encrypt(new byte[16], "texto"));
    }
}
