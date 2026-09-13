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
    public string KeyId { get; set; } = "primary";
    public Dictionary<string, string> PreviousKeys { get; set; } = [];
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
    private const string EnvelopePrefix = "enc:v2:";
    private readonly string _currentKeyId;
    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    public FieldEncryptionService(DataProtectionOptions options)
    {
        _currentKeyId = ValidarId(options.KeyId);
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [_currentKeyId] = DerivarChave(options.Key, "DataProtection:Key")
        };
        foreach (var (id, secret) in options.PreviousKeys ?? [])
        {
            var validId = ValidarId(id);
            if (!keys.TryAdd(validId, DerivarChave(secret, $"DataProtection:PreviousKeys:{validId}")))
                throw new InvalidOperationException($"Identificador de chave duplicado: {validId}.");
        }
        _keys = keys;
    }

    /// <summary>Criptografa texto plano; retorna base64 de nonce+tag+cifra.</summary>
    public string Encrypt(string plaintext)
        => $"{EnvelopePrefix}{_currentKeyId}:{AesGcmEnvelope.Encrypt(_keys[_currentKeyId], plaintext)}";

    /// <summary>Descriptografa um valor gerado por Encrypt. Retorna string vazia para valores nulos/vazios.</summary>
    public string Decrypt(string? ciphertextB64)
    {
        if (string.IsNullOrEmpty(ciphertextB64)) return "";

        if (ciphertextB64.StartsWith(EnvelopePrefix, StringComparison.Ordinal))
        {
            var separator = ciphertextB64.IndexOf(':', EnvelopePrefix.Length);
            if (separator < 0)
                throw new CryptographicException("Envelope criptográfico sem identificador de chave válido.");

            var keyId = ciphertextB64[EnvelopePrefix.Length..separator];
            if (!_keys.TryGetValue(keyId, out var selectedKey))
                throw new CryptographicException($"A chave '{keyId}' necessária para descriptografar o registro não está configurada.");
            return AesGcmEnvelope.Decrypt(selectedKey, ciphertextB64[(separator + 1)..]);
        }

        // Compatibilidade com envelopes v1, que não registravam o id da chave.
        foreach (var key in _keys.Values)
        {
            try
            {
                return AesGcmEnvelope.Decrypt(key, ciphertextB64);
            }
            catch (CryptographicException) { }
            catch (FormatException) { }
        }
        throw new CryptographicException("Nenhuma chave configurada pôde descriptografar o envelope legado.");
    }

    private static byte[] DerivarChave(string secret, string setting)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException(
                $"{setting} ausente ou fraca (mínimo 32 caracteres). Configure o segredo fora do código.");
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    private static string ValidarId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 32 ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new InvalidOperationException(
                "DataProtection:KeyId deve ter de 1 a 32 caracteres ASCII alfanuméricos, '-', '_' ou '.'.");
        return id;
    }
}
