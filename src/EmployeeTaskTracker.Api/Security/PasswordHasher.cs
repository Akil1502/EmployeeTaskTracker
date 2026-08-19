using System.Security.Cryptography;

namespace EmployeeTaskTracker.Api.Security;

/// <summary>
/// PBKDF2-SHA256 password hashing, satisfying the "password hashing" security
/// requirement.
///
/// Each hash embeds its own random salt and the iteration count it was created
/// with, in the form:
///
///     iterations.base64(salt).base64(hash)
///
/// Storing the iteration count alongside the hash means the work factor can be
/// raised later without invalidating existing passwords - old hashes still
/// verify using the count they were created with.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}

public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;      // 128-bit salt
    private const int KeySize = 32;       // 256-bit derived key
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    private const char Separator = '.';

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join(Separator,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        var segments = storedHash.Split(Separator);
        if (segments.Length != 3)
            return false;

        if (!int.TryParse(segments[0], out var iterations) || iterations <= 0)
            return false;

        byte[] salt, expectedKey;
        try
        {
            salt = Convert.FromBase64String(segments[1]);
            expectedKey = Convert.FromBase64String(segments[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);

        // Fixed-time comparison so a wrong password cannot be narrowed down by
        // timing how long the comparison takes.
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
