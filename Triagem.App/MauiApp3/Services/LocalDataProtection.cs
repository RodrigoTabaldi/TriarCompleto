using System.Security.Cryptography;
using Triagem.Core.Security;

namespace MauiApp3.Services;

/// <summary>Protege dados clínicos do SQLite com uma chave mantida pelo Keychain/Keystore/DPAPI.</summary>
internal static class LocalDataProtection
{
    private const string ChaveSecureStorage = "triar_local_data_key_v1";
    private const string Prefixo = "enc:v1:";
    private static byte[]? _key;

    public static async Task InicializarAsync()
    {
        if (_key is not null) return;

        var armazenada = await SecureStorage.Default.GetAsync(ChaveSecureStorage);
        if (!string.IsNullOrWhiteSpace(armazenada))
        {
            try
            {
                var existente = Convert.FromBase64String(armazenada);
                if (existente.Length == 32)
                {
                    _key = existente;
                    return;
                }
            }
            catch (FormatException) { }
        }

        var nova = RandomNumberGenerator.GetBytes(32);
        await SecureStorage.Default.SetAsync(ChaveSecureStorage, Convert.ToBase64String(nova));
        _key = nova;
    }

    public static string Proteger(string texto)
    {
        if (_key is null) throw new InvalidOperationException("Proteção local não inicializada.");
        if (string.IsNullOrEmpty(texto)) return texto;

        return Prefixo + AesGcmEnvelope.Encrypt(_key, texto);
    }

    public static string Desproteger(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || !valor.StartsWith(Prefixo, StringComparison.Ordinal))
            return valor ?? ""; // compatibilidade com registros anteriores à criptografia
        if (_key is null) throw new InvalidOperationException("Proteção local não inicializada.");

        return AesGcmEnvelope.Decrypt(_key, valor[Prefixo.Length..]);
    }
}
