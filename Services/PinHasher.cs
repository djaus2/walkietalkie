using System.Security.Cryptography;

namespace WalkieTalkie.Services;

public static class PinHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 120_000;

    public static (byte[] salt, byte[] hash) HashPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return (salt, hash);
    }

    public static bool VerifyPin(string pin, byte[] salt, byte[] expectedHash)
    {
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
