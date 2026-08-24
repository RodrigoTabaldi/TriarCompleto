using System.Security.Cryptography;
using System.Text;
using Triagem.Core.Security;

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
        => AesGcmEnvelope.Encrypt(_key, plaintext);

    /// <summary>Descriptografa um valor gerado por Encrypt. Retorna string vazia para valores nulos/vazios.</summary>
    public string Decrypt(string? ciphertextB64)
    {
        if (string.IsNullOrEmpty(ciphertextB64)) return "";

        return AesGcmEnvelope.Decrypt(_key, ciphertextB64);
    }
}
