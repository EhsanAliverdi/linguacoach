using System.Security.Cryptography;
using System.Text;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>Stable SHA-256 hashes used for generation idempotency fingerprints.</summary>
public static class GenerationHashing
{
    public static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
