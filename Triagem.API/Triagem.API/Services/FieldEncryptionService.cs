using System.Security.Cryptography;
using System.Text;

namespace Triagem.API.Services;

/// <summary>
/// Opções de criptografia de campos sensíveis. A chave vem de configuração/ambiente
/// (DataProtection:Key) — nunca versionada, mesma regra do Jwt:Key.
/// </summary>
public class DataProtectionOptions
{
    public string Key { get; set; } = "";
}

/// <summary>
/// Criptografia simétrica (AES-256-GCM) para campos identificáveis gravados no banco
/// (ex.: nome do paciente em TriagemResultado). SQL Server Express — usado neste
/// projeto — não suporta Transparent Data Encryption, então a proteção em repouso
/// para colunas sensíveis específicas é feita aqui, na camada de aplicação, via
/// value converter do EF Core (ver TriagemDbContext).
/// </summary>
public class FieldEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public FieldEncryptionService(DataProtectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < 32)
            throw new InvalidOperationException(
                "DataProtection:Key ausente ou fraca (mínimo 32 caracteres). Configure via variável de ambiente DataProtection__Key.");

        // SHA-256 normaliza qualquer segredo configurado (>=32 caracteres) para os 32
        // bytes exigidos pelo AES-256, sem obrigar o operador a fornecer bytes crus.
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(options.Key));
    }

    /// <summary>Criptografa texto plano; retorna base64 de nonce+tag+cifra.</summary>
    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plainBytes, cipher, tag);

        var combined = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, combined, NonceSize + TagSize, cipher.Length);

        return Convert.ToBase64String(combined);
    }

    /// <summary>Descriptografa um valor gerado por Encrypt. Retorna string vazia para valores nulos/vazios.</summary>
    public string Decrypt(string? ciphertextB64)
    {
        if (string.IsNullOrEmpty(ciphertextB64)) return "";

        var combined = Convert.FromBase64String(ciphertextB64);
        var nonce = combined.AsSpan(0, NonceSize);
        var tag = combined.AsSpan(NonceSize, TagSize);
        var cipher = combined.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
