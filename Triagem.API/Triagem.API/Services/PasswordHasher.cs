using System.Security.Cryptography;

namespace Triagem.API.Services;

/// <summary>Hash de senha PBKDF2 (SHA-256) com salt aleatório. Formato: {iterações}.{salt}.{hash}</summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string senha, string hashArmazenado)
    {
        var partes = hashArmazenado.Split('.');
        if (partes.Length != 3) return false;
        if (!int.TryParse(partes[0], out var iteracoes) || iteracoes <= 0) return false;

        byte[] salt, esperado;
        try
        {
            salt = Convert.FromBase64String(partes[1]);
            esperado = Convert.FromBase64String(partes[2]);
        }
        catch (FormatException)
        {
            // Hash armazenado corrompido/malformado: falha de autenticação limpa em
            // vez de exceção não tratada estourando até o controller.
            return false;
        }

        var key = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(key, esperado);
    }
}
