using System.Security.Cryptography;
using Application.Abstractions.Authentication;

namespace Infrastructure.Authentication;

internal sealed class PasswordHasher : IPasswordHasher
{
    private const string Version = "pbkdf2-sha512";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 500000;
    private const int MaximumAcceptedIterations = 1000000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return $"{Version}${Iterations}${Convert.ToHexString(hash)}${Convert.ToHexString(salt)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        return TryParseVersionedHash(passwordHash, out byte[] hash, out byte[] salt, out int iterations)
            ? VerifyCore(password, hash, salt, iterations)
            : TryParseLegacyHash(passwordHash, out hash, out salt) &&
              VerifyCore(password, hash, salt, Iterations);
    }

    public bool NeedsRehash(string passwordHash)
    {
        if (TryParseVersionedHash(passwordHash, out _, out _, out int iterations))
        {
            return iterations != Iterations;
        }

        return TryParseLegacyHash(passwordHash, out _, out _);
    }

    private static bool VerifyCore(string password, byte[] expectedHash, byte[] salt, int iterations)
    {
        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, HashSize);
        return CryptographicOperations.FixedTimeEquals(expectedHash, inputHash);
    }

    private static bool TryParseVersionedHash(
        string passwordHash,
        out byte[] hash,
        out byte[] salt,
        out int iterations)
    {
        string[] parts = passwordHash.Split('$');
        iterations = 0;

        if (parts.Length != 4 ||
            !string.Equals(parts[0], Version, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out iterations) ||
            iterations is < 1 or > MaximumAcceptedIterations ||
            parts[2].Length != HashSize * 2 ||
            parts[3].Length != SaltSize * 2)
        {
            hash = [];
            salt = [];
            return false;
        }

        return TryDecode(parts[2], parts[3], out hash, out salt);
    }

    private static bool TryParseLegacyHash(string passwordHash, out byte[] hash, out byte[] salt)
    {
        string[] parts = passwordHash.Split('-');

        if (parts.Length != 2 ||
            parts[0].Length != HashSize * 2 ||
            parts[1].Length != SaltSize * 2)
        {
            hash = [];
            salt = [];
            return false;
        }

        return TryDecode(parts[0], parts[1], out hash, out salt);
    }

    private static bool TryDecode(string hashHex, string saltHex, out byte[] hash, out byte[] salt)
    {
        try
        {
            hash = Convert.FromHexString(hashHex);
            salt = Convert.FromHexString(saltHex);
            return true;
        }
        catch (FormatException)
        {
            hash = [];
            salt = [];
            return false;
        }
    }
}
