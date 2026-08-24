using System.Security.Cryptography;
using System.Text;

namespace Triagem.Core.Security;

public static class AesGcmEnvelope
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string Encrypt(ReadOnlySpan<byte> key, string plaintext)
    {
        ValidarChave(key);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);

        var combinado = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combinado, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combinado, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, combinado, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(combinado);
    }

    public static string Decrypt(ReadOnlySpan<byte> key, string envelopeBase64)
    {
        ValidarChave(key);
        var combinado = Convert.FromBase64String(envelopeBase64);
        if (combinado.Length < NonceSize + TagSize)
            throw new CryptographicException("Envelope AES-GCM inválido.");

        var nonce = combinado.AsSpan(0, NonceSize);
        var tag = combinado.AsSpan(NonceSize, TagSize);
        var cipher = combinado.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static void ValidarChave(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32)
            throw new ArgumentException("A chave AES-256 deve conter 32 bytes.", nameof(key));
    }
}
