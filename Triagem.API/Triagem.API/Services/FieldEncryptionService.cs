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

    /// <summary>
    /// Chaves de uma rotação anterior, aceitas apenas para DESCRIPTOGRAFAR registros
    /// antigos — nunca usadas para gravar. Sem isto, trocar DataProtection:Key (prática
    /// de segurança normal, obrigatória se a chave vazar) tornaria ilegível todo nome de
    /// paciente já gravado com a chave anterior. Configurar via
    /// DataProtection__ChavesAnteriores__0, __1, etc.
    /// </summary>
    public string[] ChavesAnteriores { get; set; } = [];
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
    private const string MarcadorFalha = "[dado protegido — falha ao descriptografar]";

    private readonly byte[] _chaveAtual;
    private readonly IReadOnlyList<byte[]> _chavesAnteriores;
    private readonly ILogger<FieldEncryptionService>? _logger;

    public FieldEncryptionService(DataProtectionOptions options, ILogger<FieldEncryptionService>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < 32)
            throw new InvalidOperationException(
                "DataProtection:Key ausente ou fraca (mínimo 32 caracteres). Configure via variável de ambiente DataProtection__Key.");

        _chaveAtual = DerivarChave(options.Key);
        _chavesAnteriores = options.ChavesAnteriores
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(DerivarChave)
            .ToList();
        _logger = logger;
    }

    // SHA-256 normaliza qualquer segredo configurado (>=32 caracteres) para os 32
    // bytes exigidos pelo AES-256, sem obrigar o operador a fornecer bytes crus.
    private static byte[] DerivarChave(string segredo) => SHA256.HashData(Encoding.UTF8.GetBytes(segredo));

    /// <summary>Criptografa texto plano (sempre com a chave atual); retorna base64 de nonce+tag+cifra.</summary>
    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_chaveAtual, TagSize))
            aes.Encrypt(nonce, plainBytes, cipher, tag);

        var combined = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, combined, NonceSize + TagSize, cipher.Length);

        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Descriptografa um valor gerado por Encrypt, tentando a chave atual e depois as
    /// anteriores (rotação). Nunca lança: como é usado como value converter do EF Core
    /// dentro de uma projeção, uma exceção aqui abortaria a consulta inteira — uma
    /// única linha problemática (dado legado em texto plano de antes desta
    /// criptografia existir, ou cifrado com uma chave já descartada) derrubaria a
    /// página inteira do histórico com HTTP 500. Em vez disso, dados que não são um
    /// payload cifrado reconhecível voltam como vieram (compatibilidade com o texto
    /// plano legado), e um payload cifrado que nenhuma chave abre volta como um
    /// marcador seguro, com aviso registrado.
    /// </summary>
    public string Decrypt(string? valorArmazenado)
    {
        if (string.IsNullOrEmpty(valorArmazenado)) return "";

        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(valorArmazenado);
        }
        catch (FormatException)
        {
            return valorArmazenado;
        }

        if (combined.Length < NonceSize + TagSize)
            return valorArmazenado;

        foreach (var chave in ChavesEmOrdemDeTentativa())
        {
            if (TentarDescriptografar(combined, chave, out var texto))
                return texto;
        }

        _logger?.LogWarning(
            "Falha ao descriptografar campo protegido: nenhuma chave configurada (atual ou anterior) corresponde.");
        return MarcadorFalha;
    }

    private IEnumerable<byte[]> ChavesEmOrdemDeTentativa()
    {
        yield return _chaveAtual;
        foreach (var chave in _chavesAnteriores) yield return chave;
    }

    private static bool TentarDescriptografar(byte[] combined, byte[] chave, out string texto)
    {
        var nonce = combined.AsSpan(0, NonceSize);
        var tag = combined.AsSpan(NonceSize, TagSize);
        var cipher = combined.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(chave, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            texto = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch (CryptographicException)
        {
            texto = "";
            return false;
        }
    }
}
